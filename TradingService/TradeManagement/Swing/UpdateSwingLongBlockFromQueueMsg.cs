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
using TradingService.TradeManagement.Swing.Models;
using TradingService.TradeManagement.Swing.Common;

namespace TradingService.TradeManagement.Swing
{
    public class UpdateSwingLongBlockFromQueueMsg
    {
        private readonly IConfiguration _configuration;

        public UpdateSwingLongBlockFromQueueMsg(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string containerId = "Blocks";
        private static Container _container;
        private const int MaxNumShares = 50;
        private static ILogger _log;

        [FunctionName("UpdateSwingLongBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeueswinglong", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {

            _container = await Repository.GetContainer(containerId);

            _log = log;

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderUpdateMessage>(myQueueItem);
            _log.LogInformation($"Update swing block from queue msg triggered for user {orderUpdateMessage.UserId}, symbol {orderUpdateMessage.Symbol}, external order id {orderUpdateMessage.OrderId}.");

            if (orderUpdateMessage.OrderSide == OrderSide.Buy)
            {
                await UpdateBuyOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
            else
            {
                await UpdateSellOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
        }

        private async Task UpdateBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, update block to record buy order has been filled
            _log.LogInformation($"Buy order executed for trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed buy price {executedBuyPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
            if (userBlock == null)
            {
                _log.LogError($"Could not find user block for user id {userId} and symbol {symbol} at: {DateTimeOffset.Now}.");
                return;
            }

            // Update block designating buy order has been executed
            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                blockToUpdate.BuyOrderFilled = true;
                blockToUpdate.DateBuyOrderFilled = DateTime.Now;
                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;
                blockToUpdate.SellOrderCreated = true;
            }
            else
            {
                _log.LogError($"Could not find block for buy user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                return;
            }

            var userBlockReplaceResponse = await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
            _log.LogInformation($"Saved block id {blockToUpdate.Id} to DB with sell order created flag to true at: {DateTimeOffset.Now}.");

        }

        private async Task UpdateSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, close and reset block
            _log.LogInformation($"Sell order executed for trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId} executed sell price {executedSellPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);

            if (userBlock == null)
            {
                _log.LogError($"Could not find user block for user id {userId} and symbol {symbol} at: {DateTimeOffset.Now}.");
                return;
            }

            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);
            if (blockToUpdate != null)
            {
                _log.LogInformation($"Sell order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Check if the buy order has been executed, if not, this is an error state
                var retryAttemptCount = 1;
                const int maxAttempts = 3;
                while (!blockToUpdate.BuyOrderFilled && retryAttemptCount <= maxAttempts)
                {
                    await Task.Delay(1000); // Wait one second in between attempts
                    _log.LogError($"Error while updating sell order executed. Buy order has not had BuyOrderFilled flag set to true yet. Retry attempt {retryAttemptCount}");
                    userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
                    blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);
                    retryAttemptCount += 1;
                }

                blockToUpdate.SellOrderFilledPrice = executedSellPrice;

                // Put message on a queue to be processed by a different function
                await TradeManagementCommon.CreateClosedBlockMsg(_log, _configuration, userBlock, blockToUpdate);

                // Replace block with new orders
                var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userBlock.UserId,
                    userBlock.Symbol, userBlock.NumShares,
                    blockToUpdate.BuyOrderPrice, blockToUpdate.SellOrderPrice, blockToUpdate.StopLossOrderPrice);
                _log.LogInformation(
                    $"Replacement buy order created for block id {blockToUpdate.Id}, symbol {userBlock.Symbol}, external buy id {orderIds.ParentOrderId}, external sell id {orderIds.TakeProfitId}, external stop id {orderIds.StopLossOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Reset block
                //TESTblock.ExternalBuyOrderId = new Guid("00000000-0000-0000-0000-000000000001");
                blockToUpdate.ExternalBuyOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = true;
                blockToUpdate.BuyOrderFilled = false;
                blockToUpdate.BuyOrderFilledPrice = 0;
                blockToUpdate.DateBuyOrderFilled = DateTime.MinValue;
                blockToUpdate.SellOrderCreated = false;
                blockToUpdate.SellOrderFilled = false;
                blockToUpdate.SellOrderFilledPrice = 0;
                blockToUpdate.DateSellOrderFilled = DateTime.MinValue;

                // Replace the item with the updated content
                var blockReplaceResponse =
                    await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                _log.LogInformation($"Block reset for block id {blockToUpdate.Id} symbol {userBlock.Symbol} and saved to DB at: {DateTimeOffset.Now}.");
            }
            else
            {
                _log.LogError($"Could not find block for sell for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
            }
        }
    }
}
