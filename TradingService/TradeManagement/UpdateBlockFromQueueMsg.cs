using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.TradeManagement.Models;

namespace TradingService.TradeManagement
{
    public static class UpdateBlockFromQueueMsg
    {
        private static readonly string endpointUri = Environment.GetEnvironmentVariable("EndPointUri"); // ToDo: Centralize config values to common project?
        private static readonly string primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

        private static readonly string databaseId = "Tracker";
        private static readonly string containerId = "Blocks";
        private static readonly string containerArchiveId = "BlocksArchive";

        private static readonly CosmosClient cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
        private static Database _database;
        private static Container _container;
        private static Container _containerArchive;

        private const int MaxNumShares = 50;

        private static ILogger _log;

        [FunctionName("UpdateBlockFromQueueMsg")]
        public static async Task Run([QueueTrigger("tradeupdatequeue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(containerId, "/symbol");
            _containerArchive = await _database.CreateContainerIfNotExistsAsync(containerArchiveId, "/symbol");
            _log = log;

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderUpdateMessage>(myQueueItem);

            if (orderUpdateMessage.OrderSide == OrderSide.Buy)
            {
                await UpdateBuyOrderExecuted(orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
            else
            {
                await UpdateSellOrderExecuted(orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }

            _log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
            
        }

        private static async Task UpdateBuyOrderExecuted(Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, create sell order in Alpaca and update block to record buy and sell order id from Alpaca
            // Get block item by external id
            var block = await GetBlockByExternalBuyOrderId(externalOrderId);
            if (block == null)
            {
                _log.LogError("Could not find block for external buy order id {externalOrderId} at: {time}", externalOrderId, DateTimeOffset.Now);
                return;
            }
            _log.LogInformation("Buy order has been executed for block id {id}, external buy id {externalOrderId} at: {time}", block.Id, externalOrderId, DateTimeOffset.Now);
            // Update with external buy id generated from Alpaca
            block.BuyOrderFilled = true;
            block.DateBuyOrderFilled = DateTime.Now;
            block.BuyOrderFilledPrice = executedBuyPrice;
            block.SellOrderCreated = true;
            //TESTblock.ExternalSellOrderId = new Guid("00000000-0000-0000-0000-000000000002");

            // Replace the item with the updated content
            var blockReplaceResponse = await _container.ReplaceItemAsync<Block>(block, block.Id, new PartitionKey(block.Symbol));
            _log.LogInformation("Saved block id {blockId} to DB with sell order created flag to true at: {time}", block.Id, DateTimeOffset.Now);
            
            if (!block.DayBlock)
            {
                // Check if buy order above current block has been created, if not, create it
                await CreateBuyOrderAboveIfNotCreated(block.Id, block.Symbol);

                // Check if buy order below current block has been created, if not, create it
                await CreateBuyOrderBelowIfNotCreated(block.Id, block.Symbol);

            }
        }

        private static async Task UpdateSellOrderExecuted(Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, archive and reset block
            var block = await GetBlockByExternalSellOrderId(externalOrderId);
            if (block == null)
            {
                _log.LogError("Could not find block for external sell order id {externalOrderId} at: {time}", externalOrderId, DateTimeOffset.Now);
                return;
            }

            _log.LogInformation("Sell order has been executed for block id {id}, external id {externalOrderId} and saved to DB at: {time}", block.Id, externalOrderId, DateTimeOffset.Now);

            // Archive block -- ToDo: Create a new queue and function to trigger to archive block
            await ArchiveBlock(block, executedSellPrice);
            _log.LogInformation("Created archive record for block id {id} at: {time}", block.Id, DateTimeOffset.Now);

            // Create a new buy limit order in Alpaca for replacement
            // Up the number of shares each time a block gets replaced as confidence goes up
            //block.NumShares += 5;

            //if (block.NumShares > MaxNumShares)
            //{
            //    block.NumShares = MaxNumShares;
            //}

            if (!block.DayBlock)
            {
                var orderIds = await Order.CreateLimitBracketOrder(OrderSide.Buy, block.Symbol, block.NumShares,
                    block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
                _log.LogInformation(
                    "Replacement buy order created for block id {id}, symbol {symbol}, external buy id {externalBuyId}, external sell id {externalSellId}, external stop id {externalStopId} and saved to DB at: {time}",
                    block.Id, block.Symbol, orderIds.BuyOrderId, orderIds.SellOrderId, orderIds.StopLossOrderId, DateTimeOffset.Now);

                // Reset block
                //TESTblock.ExternalBuyOrderId = new Guid("00000000-0000-0000-0000-000000000001");
                block.ExternalBuyOrderId = orderIds.BuyOrderId;
                block.ExternalSellOrderId = orderIds.SellOrderId;
                block.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                block.BuyOrderFilled = false;
                block.BuyOrderFilledPrice = 0;
                block.DateBuyOrderFilled = DateTime.MinValue;
                block.SellOrderCreated = false;
                block.SellOrderFilledPrice = 0;

                // Replace the item with the updated content
                var blockReplaceResponse =
                    await _container.ReplaceItemAsync<Block>(block, block.Id, new PartitionKey(block.Symbol));

                _log.LogInformation("Block reset for block id {id} symbol {symbol} and saved to DB at: {time}", block.Id, block.Symbol,
                    DateTimeOffset.Now);
            }
        }

        private static async Task<Block> GetBlockByExternalBuyOrderId(Guid externalOrderId)
        {
            // Read blocks from Cosmos DB
            var blocks = new List<Block>();
            try
            {
                using var setIterator = _container.GetItemLinqQueryable<Block>()
                    .Where(b => b.ExternalBuyOrderId == externalOrderId)
                    .ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    blocks.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                _log.LogError("Issue creating Cosmos DB item {ex}", ex);
            }

            return blocks.FirstOrDefault();
        }

        private static async Task<Block> GetBlockByExternalSellOrderId(Guid externalOrderId)
        {
            // Read blocks from Cosmos DB
            var blocks = new List<Block>();
            try
            {
                using var setIterator = _container.GetItemLinqQueryable<Block>()
                    .Where(b => b.ExternalSellOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId)
                    .ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    blocks.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                _log.LogError("Issue creating Cosmos DB item {ex}", ex);
            }

            return blocks.FirstOrDefault();
        }

        private static async Task<Block> GetBlockById(long id, string symbol)
        {
            try
            {
                return await _container.ReadItemAsync<Block>(id.ToString(), new PartitionKey(symbol));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static async Task CreateBuyOrderAboveIfNotCreated(string currentBlockId, string symbol)
        {
            // Get block above and create new buy order if no order has been created, adjust number of shares up since going up (put more in on uptrend)
            var blockAboveId = (Convert.ToInt64(currentBlockId) + 1);
            var blockAbove = await GetBlockById(blockAboveId, symbol);
            var stopPrice = blockAbove.BuyOrderPrice - (decimal)0.01;

            // If no buy order has been created, create one
            if (!blockAbove.BuyOrderCreated)
            {
                // Up the number of shares each time a order is created going up
                //blockAbove.NumShares += 5;

                //if (blockAbove.NumShares > MaxNumShares)
                //{
                //    blockAbove.NumShares = MaxNumShares;
                //}

                // Create new buy order
                var orderIds = await Order.CreateStopLimitBracketOrder(OrderSide.Buy, symbol, blockAbove.NumShares, stopPrice, blockAbove.BuyOrderPrice, blockAbove.SellOrderPrice, blockAbove.StopLossOrderPrice);
                _log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", symbol, blockAbove.BuyOrderPrice);

                // Replace Cosmos DB document, update with external buy id generated from Alpaca
                blockAbove.ExternalBuyOrderId = orderIds.BuyOrderId;
                blockAbove.ExternalSellOrderId = orderIds.SellOrderId;
                blockAbove.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockAbove.BuyOrderCreated = true;

                var updateBlockResponse = await _container.ReplaceItemAsync<Block>(blockAbove, blockAbove.Id, new PartitionKey(blockAbove.Symbol));
                _log.LogInformation($"A new buy order has been placed for block id {blockAbove.Id}. The external order id is { blockAbove.ExternalBuyOrderId}",
                    blockAbove.Id, blockAbove.ExternalBuyOrderId);
            }
        }

        private static async Task CreateBuyOrderBelowIfNotCreated(string currentBlockId, string symbol)
        {
            // Get block below and create a new buy order if no order has been created, adjust number of shares down since going down (put less in on downtrend)
            var blockBelowId = (Convert.ToInt64(currentBlockId) - 1);
            var blockBelow = await GetBlockById(blockBelowId, symbol);

            // If no buy order has been created, create one
            if (!blockBelow.BuyOrderCreated)
            {
                // Create new buy order
                var orderIds = await Order.CreateLimitBracketOrder(OrderSide.Buy, symbol, blockBelow.NumShares, blockBelow.BuyOrderPrice, blockBelow.SellOrderPrice, blockBelow.StopLossOrderPrice);

                // Replace Cosmos DB document, update with external buy id generated from Alpaca
                blockBelow.ExternalBuyOrderId = orderIds.BuyOrderId;
                blockBelow.ExternalSellOrderId = orderIds.SellOrderId;
                blockBelow.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockBelow.BuyOrderCreated = true;

                var updateBlockResponse = await _container.ReplaceItemAsync<Block>(blockBelow, blockBelow.Id,
                    new PartitionKey(blockBelow.Symbol));
                _log.LogInformation(
                    $"A new buy order has been placed for block id {blockBelow.Id}. The external order id is {blockBelow.ExternalBuyOrderId}",
                    blockBelow.Id, blockBelow.ExternalBuyOrderId);
            }
        }

        private static async Task ArchiveBlock(Block block, decimal executedSellPrice)
        {
            // ToDo: Create a new object for archive block, only keep the fields relevant to archive, add profit field
            var archiveBlockJson = JsonConvert.SerializeObject(block);
            var archiveBlock = JsonConvert.DeserializeObject<Block>(archiveBlockJson); //deep copy object
            archiveBlock.Id = Guid.NewGuid().ToString();
            archiveBlock.SellOrderFilledPrice = executedSellPrice;

            await _containerArchive.CreateItemAsync<Block>(archiveBlock, new PartitionKey(archiveBlock.Symbol));
        }

    }
}
