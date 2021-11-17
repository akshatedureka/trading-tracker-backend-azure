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

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderUpdateMessage>(myQueueItem);
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
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            // Update block designating buy order has been executed
            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);

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

            var userBlockReplaceResponse = await _container.ReplaceItemAsync(blockToUpdate, blockToUpdate.Id, new PartitionKey(userId));
            _log.LogInformation($"Saved block id {blockToUpdate.Id} to DB with sell order created flag to true at: {DateTimeOffset.Now}.");

        }

        private async Task UpdateBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, update block to record buy order has been filled
            _log.LogInformation($"Buy order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed buy price {executedBuyPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade blocks
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            // Update block designating buy order has been executed
            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);

            // Retry 3 times to get block to allow for records to be updated
            var retryAttemptCount = 1;
            const int maxAttempts = 3;
            while (blockToUpdate == null && retryAttemptCount <= maxAttempts)
            {
                await Task.Delay(1000); // Wait one second in between attempts
                _log.LogError($"Could not find block with external buy order id {externalOrderId}. Retry attempt {retryAttemptCount}");
                blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);
                blockToUpdate = blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);
                retryAttemptCount += 1;
            }

            if (blockToUpdate != null)
            {
                _log.LogInformation($"Buy order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");
                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;

                // Put message on a queue to be processed by a different function from a message queue
                await TradeManagementCommon.CreateClosedBlockMsg(_log, _configuration, blockToUpdate);

                // Replace block with new order
                //var stopLossPrice = blockToUpdate.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down
                //var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, blockToUpdate.SellOrderPrice, blockToUpdate.BuyOrderPrice, stopLossPrice);

                //_log.LogInformation(
                //    $"Replacement sell order created for block id {blockToUpdate.Id}, symbol {userBlock.Symbol}, external buy id {orderIds.TakeProfitId}, external sell id {orderIds.ParentOrderId}, external stop id {orderIds.StopLossOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Reset block
                await TradeManagementCommon.CreateResetBlockMsg(_log, _configuration, blockToUpdate);
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

                _log.LogInformation($"Message queued to reset short block id {blockToUpdate.Id} symbol {blockToUpdate.Symbol} at: {DateTimeOffset.Now}.");
            }
            else
            {
                _log.LogError($"Could not find block for buy for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
            }
        }
    }
}
