using System;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.TradeManagement.Swing.Common;
using TradingService.TradeManagement.Swing.Models;

namespace TradingService.TradeManagement.Swing
{
    public class UpdateSwingShortBlockFromQueueMsg
    {
        private ILogger _log;
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly string containerId = "Blocks";
        private Container _container;

        public UpdateSwingShortBlockFromQueueMsg(IConfiguration configuration, IRepository repository, IQueries queries)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
        }

        [FunctionName("UpdateSwingShortBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeueswingshort", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _log = log;
            _log.LogInformation($"Update swing short block from queue msg triggered at {DateTime.Now}.");
            
            _container = await _repository.GetContainer(containerId);

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderMessage>(myQueueItem);
            if (orderUpdateMessage.IsOrderCreation)
                return;
            _log.LogInformation($"Update swing short block from queue msg triggered for user {orderUpdateMessage.UserId}, symbol {orderUpdateMessage.Symbol}, external order id {orderUpdateMessage.OrderId}.");

            if (orderUpdateMessage.OrderSide == OrderSide.Buy)
            {
                await UpdateBuyOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
            else
            {
                await UpdateSellOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
        }

        private async Task UpdateSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, close and reset block
            _log.LogInformation($"Sell order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed sell price {executedSellPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade blocks
            var userBlock = await _queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
            if (userBlock == null)
            {
                _log.LogError($"Could not find user block for user id {userId} and symbol {symbol} at: {DateTimeOffset.Now}.");
                return;
            }

            // Update block designating sell order has been executed
            //ToDo: Block could be found using either sell order or stop loss order id's
            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                blockToUpdate.SellOrderFilled = true;
                blockToUpdate.DateSellOrderFilled = DateTime.Now;
                blockToUpdate.SellOrderFilledPrice = executedSellPrice;
                blockToUpdate.BuyOrderCreated = true;
            }
            else
            {
                _log.LogError($"Could not find block for sell user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                return;
            }

            var userBlockReplaceResponse = await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
            _log.LogInformation($"Saved block id {blockToUpdate.Id} to DB with sell order created flag to true at: {DateTimeOffset.Now}.");
        }

        private async Task UpdateBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, update block to record buy order has been filled
            _log.LogInformation($"Buy order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed buy price {executedBuyPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var userBlock = await _queries.GetUserBlockByUserIdAndSymbol(userId, symbol);

            if (userBlock == null)
            {
                _log.LogError($"Could not find user block for user id {userId} and symbol {symbol} at: {DateTimeOffset.Now}.");
                return;
            }

            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                _log.LogInformation($"Buy order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Retry 3 times to get block to allow for records to be updated
                var retryAttemptCount = 1;
                const int maxAttempts = 3;
                while (!blockToUpdate.SellOrderFilled && retryAttemptCount <= maxAttempts)
                {
                    await Task.Delay(1000); // Wait one second in between attempts
                    _log.LogError($"Error while updating buy order executed. Sell order has not had SellOrderFilled flag set to true yet. Retry attempt {retryAttemptCount}");
                    userBlock = await _queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
                    blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);
                    retryAttemptCount += 1;
                }

                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;

                // Put message on a queue to be processed by a different function from a message queue
                await TradeManagementCommon.CreateClosedBlockMsg(_log, _configuration, userBlock, blockToUpdate);

                // Replace block with new order
                //var stopLossPrice = blockToUpdate.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down
                //var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, blockToUpdate.SellOrderPrice, blockToUpdate.BuyOrderPrice, stopLossPrice);

                //_log.LogInformation(
                //    $"Replacement sell order created for block id {blockToUpdate.Id}, symbol {userBlock.Symbol}, external buy id {orderIds.TakeProfitId}, external sell id {orderIds.ParentOrderId}, external stop id {orderIds.StopLossOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Reset block
                await TradeManagementCommon.CreateResetBlockMsg(_log, _configuration, userBlock, blockToUpdate);
                //blockToUpdate.ExternalBuyOrderId = new Guid();
                //blockToUpdate.ExternalSellOrderId = new Guid();
                //blockToUpdate.ExternalStopLossOrderId = new Guid();
                //blockToUpdate.BuyOrderCreated = false;
                //blockToUpdate.BuyOrderFilled = false;
                //blockToUpdate.BuyOrderFilledPrice = 0;
                //blockToUpdate.DateBuyOrderFilled = DateTime.MinValue;
                //blockToUpdate.SellOrderCreated = false;
                //blockToUpdate.SellOrderFilled = false;
                //blockToUpdate.SellOrderFilledPrice = 0;
                //blockToUpdate.DateSellOrderFilled = DateTime.MinValue;

                // Replace the item with the updated content
                //var blockReplaceResponse =
                //    await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                _log.LogInformation($"Message queued to reset short block id {blockToUpdate.Id} symbol {symbol} at: {DateTimeOffset.Now}.");
            }
            else
            {
                _log.LogError($"Could not find block for buy for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                throw new Exception("Could not find block.");
            }
        }
    }
}
