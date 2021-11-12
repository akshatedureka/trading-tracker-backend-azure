using System;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.TradeManagement.Swing.Common;
using TradingService.TradeManagement.Swing.Models;

namespace TradingService.TradeManagement.Swing
{
    public class UpdateSwingShortBlockFromQueueMsg
    {
        private readonly IConfiguration _configuration;

        public UpdateSwingShortBlockFromQueueMsg(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string containerId = "Blocks";
        private static Container _container;
        private const int MaxNumShares = 50;
        private static ILogger _log;

        [FunctionName("UpdateSwingShortBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeueswingshort", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {

            _container = await Repository.GetContainer(containerId);

            _log = log;

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
            // Sell order has been executed, create new buy order in Alpaca, archive and reset block
            _log.LogInformation($"Sell order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed sell price {executedSellPrice} at: {DateTimeOffset.Now}.");
                       
            // Get swing trade block
            var userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
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
            var userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);

            if (userBlock == null)
            {
                _log.LogError($"Could not find user block for user id {userId} and symbol {symbol} at: {DateTimeOffset.Now}.");
                return;
            }

            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);
            if (blockToUpdate != null)
            {
                _log.LogInformation($"Buy order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Check if the sell order has been executed, if not, this is an error state
                var retryAttemptCount = 1;
                const int maxAttempts = 3;
                while (!blockToUpdate.SellOrderFilled && retryAttemptCount <= maxAttempts)
                {
                    await Task.Delay(1000); // Wait one second in between attempts
                    _log.LogError($"Error while updating buy order executed. Sell order has not had SellOrderFilled flag set to true yet. Retry attempt {retryAttemptCount}");
                    userBlock = await Queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
                    blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);
                    retryAttemptCount += 1;
                }

                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;

                // Put message on a queue to be processed by a different function
                await TradeManagementCommon.CreateArchiveBlockMsg( _log, _configuration, userBlock, blockToUpdate);

                // Replace block with new order
                var stopLossPrice = blockToUpdate.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down
                var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, blockToUpdate.SellOrderPrice, blockToUpdate.BuyOrderPrice, stopLossPrice);

                _log.LogInformation(
                    $"Replacement sell order created for block id {blockToUpdate.Id}, symbol {userBlock.Symbol}, external buy id {orderIds.TakeProfitId}, external sell id {orderIds.ParentOrderId}, external stop id {orderIds.StopLossOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Reset block
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = false;
                blockToUpdate.BuyOrderFilled = false;
                blockToUpdate.BuyOrderFilledPrice = 0;
                blockToUpdate.DateBuyOrderFilled = DateTime.MinValue;
                blockToUpdate.SellOrderCreated = true;
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
                _log.LogError($"Could not find block for buy for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
            }
        }

        private async Task<BracketOrderIds> CreateSellOrderAboveIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            var stopLossPrice = block.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down

            // If no sell order has been created, create one
            if (block.SellOrderCreated) return null;

            // Create new sell order
            var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, numShares, block.SellOrderPrice, block.BuyOrderPrice, stopLossPrice);
            _log.LogInformation($"A new bracket limit order has been placed for block id {block.Id}. The external sell order id is {orderIds.ParentOrderId}.");

            return orderIds;
        }

        private async Task<BracketOrderIds> CreateSellOrderBelowIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            // If no sell order has been created, create one
            if (block.SellOrderCreated) return null;

            var stopPrice = block.SellOrderPrice + (decimal)0.05;
            var stopLossPrice = block.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down

            // Create new sell order
            var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, numShares, stopPrice, block.SellOrderPrice, block.BuyOrderPrice, stopLossPrice);
            _log.LogInformation($"A new bracket stop limit order has been placed for block id {block.Id}. The external sell order id is {orderIds.ParentOrderId}.");

            return orderIds;
        }
    }
}
