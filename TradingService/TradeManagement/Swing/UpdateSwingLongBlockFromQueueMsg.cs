using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.TradeManagement.Swing.Models;

namespace TradingService.TradeManagement.Swing
{
    public class UpdateSwingLongBlockFromQueueMsg
    {
        private readonly IConfiguration _configuration;

        public UpdateSwingLongBlockFromQueueMsg(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string databaseId = "Tracker";
        private static readonly string containerId = "Blocks";
        private static readonly string containerArchiveId = "BlocksArchive";
        private static Container _container;
        private static Container _containerArchive;
        private const int MaxNumShares = 50;
        private static ILogger _log;

        [FunctionName("UpdateSwingLongBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeueswinglong", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {

            _container = await Repository.GetContainer(databaseId, containerId);
            _containerArchive = await Repository.GetContainer(databaseId, containerArchiveId);

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

            var currentBlockId = Convert.ToInt64(blockToUpdate.Id);
            var blockAbove = userBlock.Blocks.FirstOrDefault(b => b.Id == (currentBlockId + 1).ToString());
            var blockBelow = userBlock.Blocks.FirstOrDefault(b => b.Id == (currentBlockId - 1).ToString());

            // Check if buy order above current block has been created, if not, create it
            var orderIdsAbove = await CreateBuyOrderAboveIfNotCreated(blockAbove, userBlock.UserId, userBlock.Symbol, userBlock.NumShares);

            if (orderIdsAbove != null)
            {
                // Update block with new orderIds in DB
                blockAbove.ExternalBuyOrderId = orderIdsAbove.ParentOrderId;
                blockAbove.ExternalSellOrderId = orderIdsAbove.TakeProfitId;
                blockAbove.ExternalStopLossOrderId = orderIdsAbove.StopLossOrderId;
                blockAbove.BuyOrderCreated = true;
            }

            // ToDo: Refactor to separate function 
            // Check if buy order below current block has been created, if not, create it
            var orderIdsBelow = await CreateBuyOrderBelowIfNotCreated(blockBelow, userBlock.UserId, userBlock.Symbol, userBlock.NumShares);

            if (orderIdsBelow != null)
            {
                // Update block with new orderIds in DB
                blockBelow.ExternalBuyOrderId = orderIdsBelow.ParentOrderId;
                blockBelow.ExternalSellOrderId = orderIdsBelow.TakeProfitId;
                blockBelow.ExternalStopLossOrderId = orderIdsBelow.StopLossOrderId;
                blockBelow.BuyOrderCreated = true;
            }

            var userBlockReplaceResponse = await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
            _log.LogInformation($"Saved block id {currentBlockId} to DB with sell order created flag to true at: {DateTimeOffset.Now}.");

        }

        private async Task UpdateSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, archive and reset block
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

                // Archive block -- ToDo: Create a new queue and function to trigger to archive block
                // Put message on a queue to be processed by a different function

                await ArchiveBlock(userBlock, blockToUpdate);
                _log.LogInformation($"Created archive record for block id {blockToUpdate.Id} at: { DateTimeOffset.Now}.");

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

        private async Task<BracketOrderIds> CreateBuyOrderAboveIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            var stopPrice = block.BuyOrderPrice - (decimal)0.05;

            // If no buy order has been created, create one
            if (block.BuyOrderCreated) return null;

            // Create new buy order
            var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Buy, userId, symbol, numShares, stopPrice, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
            _log.LogInformation($"Created long bracket order for symbol {symbol} for limit price {block.BuyOrderPrice}.");

            return orderIds;
        }

        private async Task<BracketOrderIds> CreateBuyOrderBelowIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            // If no buy order has been created, create one
            if (block.BuyOrderCreated) return null;

            // Create new buy order
            var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userId, symbol, numShares, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
            _log.LogInformation($"Created long bracket order for symbol {symbol} for limit price {block.BuyOrderPrice}.");

            return orderIds;
        }

        private async Task ArchiveBlock(UserBlock userBlock, Block block)
        {
            var archiveBlock = new ArchiveBlock()
            {
                Id = Guid.NewGuid().ToString(),
                BlockId = block.Id,
                DateCreated = DateTime.Now,
                UserId = userBlock.UserId,
                Symbol = userBlock.Symbol,
                NumShares = userBlock.NumShares,
                ExternalBuyOrderId = block.ExternalBuyOrderId,
                ExternalSellOrderId = block.ExternalSellOrderId,
                ExternalStopLossOrderId = block.ExternalStopLossOrderId,
                BuyOrderFilledPrice = block.BuyOrderFilledPrice,
                DateBuyOrderFilled = block.DateBuyOrderFilled,
                DateSellOrderFilled = DateTime.Now,
                SellOrderFilledPrice = block.SellOrderFilledPrice,
                IsShort = false,
                Profit = (block.SellOrderFilledPrice - block.BuyOrderFilledPrice) * userBlock.NumShares
            };

            await _containerArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
        }
    }
}
