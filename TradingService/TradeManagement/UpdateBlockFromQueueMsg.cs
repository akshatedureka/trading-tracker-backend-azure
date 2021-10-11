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
using TradingService.TradeManagement.Models;

namespace TradingService.TradeManagement
{
    public class UpdateBlockFromQueueMsg
    {
        private readonly IConfiguration _configuration;

        public UpdateBlockFromQueueMsg(IConfiguration configuration)
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

        [FunctionName("UpdateBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _container = await Repository.GetContainer(databaseId, containerId);
            _containerArchive = await Repository.GetContainer(databaseId, containerArchiveId);
            _log = log;

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderUpdateMessage>(myQueueItem);

            if (orderUpdateMessage.OrderSide == OrderSide.Buy)
            {
                await UpdateBuyOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
            else
            {
                await UpdateSellOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }

            _log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

        }

        private async Task UpdateBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, update block to record buy order has been filled
            var userBlock = await GetUserBlockByUserIdAndSymbol(userId, symbol);
            if (userBlock == null)
            {
                _log.LogError("Could not find user block for user id {userId} and symbol {symbol} at: {time}", userId, symbol, DateTimeOffset.Now);
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
                _log.LogError("Could not find block for user id {userId} and symbol {symbol} at: {time}", userId, symbol, DateTimeOffset.Now);
                return;
            }
            _log.LogInformation("Buy order has been executed for block id {blockId} for user id {user id} and symbol {symbol} at: {time}", blockToUpdate.Id, userId, symbol, DateTimeOffset.Now);


            //TESTblock.ExternalSellOrderId = new Guid("00000000-0000-0000-0000-000000000002");

            if (!blockToUpdate.DayBlock)
            {
                var currentBlockId = Convert.ToInt64(blockToUpdate.Id);
                var blockAbove = userBlock.Blocks.FirstOrDefault(b => b.Id == (currentBlockId + 1).ToString());
                var blockBelow = userBlock.Blocks.FirstOrDefault(b => b.Id == (currentBlockId - 1).ToString());

                // Check if buy order above current block has been created, if not, create it
                var orderIdsAbove = await CreateBuyOrderAboveIfNotCreated(blockAbove, userBlock.UserId, userBlock.Symbol, userBlock.NumShares);

                if (orderIdsAbove != null)
                {
                    // Update block with new orderIds in DB
                    blockAbove.ExternalBuyOrderId = orderIdsAbove.BuyOrderId;
                    blockAbove.ExternalSellOrderId = orderIdsAbove.SellOrderId;
                    blockAbove.ExternalStopLossOrderId = orderIdsAbove.StopLossOrderId;
                    blockAbove.BuyOrderCreated = true;
                }

                // Check if buy order below current block has been created, if not, create it
                var orderIdsBelow = await CreateBuyOrderBelowIfNotCreated(blockBelow, userBlock.UserId, userBlock.Symbol, userBlock.NumShares);

                if (orderIdsBelow != null)
                {
                    // Update block with new orderIds in DB
                    blockBelow.ExternalBuyOrderId = orderIdsBelow.BuyOrderId;
                    blockBelow.ExternalSellOrderId = orderIdsBelow.SellOrderId;
                    blockBelow.ExternalStopLossOrderId = orderIdsBelow.StopLossOrderId;
                    blockBelow.BuyOrderCreated = true;
                }

                var userBlockReplaceResponse = await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
                _log.LogInformation("Saved block id {blockId} to DB with sell order created flag to true at: {time}", userBlock.Id, DateTimeOffset.Now);
            }
        }

        private async Task UpdateSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, archive and reset block
            var userBlock = await GetUserBlockByUserIdAndSymbol(userId, symbol);

            if (userBlock == null)
            {
                _log.LogError("Could not find user block for user id {userId} and symbol {symbol} at: {time}", userId, symbol, DateTimeOffset.Now);
                return;
            }

            var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);
            blockToUpdate.SellOrderFilledPrice = executedSellPrice;

            _log.LogInformation("Sell order has been executed for block id {id}, external id {externalOrderId} and saved to DB at: {time}", blockToUpdate.Id, externalOrderId, DateTimeOffset.Now);

            // Archive block -- ToDo: Create a new queue and function to trigger to archive block
            // Put message on a queue to be processed by a different function

            await ArchiveBlock(userBlock, blockToUpdate);
            _log.LogInformation("Created archive record for block id {id} at: {time}", blockToUpdate.Id, DateTimeOffset.Now);

            if (!blockToUpdate.DayBlock)
            {
                // Replace block with new orders
                var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userBlock.UserId, userBlock.Symbol, userBlock.NumShares,
                    blockToUpdate.BuyOrderPrice, blockToUpdate.SellOrderPrice, blockToUpdate.StopLossOrderPrice);
                _log.LogInformation(
                    "Replacement buy order created for block id {id}, symbol {symbol}, external buy id {externalBuyId}, external sell id {externalSellId}, external stop id {externalStopId} and saved to DB at: {time}",
                    blockToUpdate.Id, userBlock.Symbol, orderIds.BuyOrderId, orderIds.SellOrderId, orderIds.StopLossOrderId, DateTimeOffset.Now);

                // Reset block
                //TESTblock.ExternalBuyOrderId = new Guid("00000000-0000-0000-0000-000000000001");
                blockToUpdate.ExternalBuyOrderId = orderIds.BuyOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.SellOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderFilled = false;
                blockToUpdate.BuyOrderFilledPrice = 0;
                blockToUpdate.DateBuyOrderFilled = DateTime.MinValue;
                blockToUpdate.SellOrderCreated = false;
                blockToUpdate.SellOrderFilledPrice = 0;

                // Replace the item with the updated content
                var blockReplaceResponse =
                    await _container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                _log.LogInformation("Block reset for block id {id} symbol {symbol} and saved to DB at: {time}", blockToUpdate.Id, userBlock.Symbol,
                    DateTimeOffset.Now);
            }
        }

        private async Task<UserBlock> GetUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            // Read user blocks from Cosmos DB
            var userBlocks = new List<UserBlock>();
            try
            {
                using var setIterator = _container.GetItemLinqQueryable<UserBlock>()
                    .Where(b => b.UserId == userId && b.Symbol == symbol)
                    .ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    userBlocks.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                _log.LogError("Issue getting user block from Cosmos DB item {ex}", ex);
            }

            return userBlocks.FirstOrDefault();
        }

        //private async Task<Block> GetBlockByExternalSellOrderId(Guid externalOrderId)
        //{
        //    // Read blocks from Cosmos DB
        //    var blocks = new List<Block>();
        //    try
        //    {
        //        using var setIterator = _container.GetItemLinqQueryable<Block>()
        //            .Where(b => b.ExternalSellOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId)
        //            .ToFeedIterator();
        //        while (setIterator.HasMoreResults)
        //        {
        //            blocks.AddRange(await setIterator.ReadNextAsync());
        //        }
        //    }
        //    catch (CosmosException ex)
        //    {
        //        _log.LogError("Issue creating Cosmos DB item {ex}", ex);
        //    }

        //    return blocks.FirstOrDefault();
        //}

        //private async Task<UserBlock> GetBlockById(long id, string symbol)
        //{
        //    try
        //    {
        //        return await _container.ReadItemAsync<UserBlock>(id.ToString(), new PartitionKey(symbol));
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        throw;
        //    }
        //}

        private async Task<BracketOrderIds> CreateBuyOrderAboveIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            var stopPrice = block.BuyOrderPrice - (decimal)0.05;

            // If no buy order has been created, create one
            if (block.BuyOrderCreated) return null;

            // Create new buy order
            var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Buy, userId, symbol, numShares, stopPrice, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
            _log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", symbol, block.BuyOrderPrice);

            return orderIds;
        }

        private async Task<BracketOrderIds> CreateBuyOrderBelowIfNotCreated(Block block, string userId, string symbol, long numShares)
        {
            // If no buy order has been created, create one
            if (block.BuyOrderCreated) return null;

            // Create new buy order
            var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userId, symbol, numShares, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
            _log.LogInformation(
                $"A new buy order has been placed for block id {block.Id}. The external order id is {block.ExternalBuyOrderId}",
                block.Id, block.ExternalBuyOrderId);

            return orderIds;
        }

        private async Task ArchiveBlock(UserBlock userBlock, Block block)
        {
            var archiveBlock = new ArchiveBlock()
            {
                Id = Guid.NewGuid().ToString(),
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
                DayBlock = false,
                Profit = block.SellOrderFilledPrice - block.BuyOrderFilledPrice
            };

            await _containerArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
        }
    }
}
