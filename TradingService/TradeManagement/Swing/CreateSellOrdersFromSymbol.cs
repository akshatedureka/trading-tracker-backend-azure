using System;
using System.Collections.Generic;
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
using TradingService.TradeManagement.Swing.Models;

namespace TradingService.TradeManagement.Swing
{
    public class CreateSellOrdersFromSymbol
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public CreateSellOrdersFromSymbol(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        [FunctionName("CreateSellOrdersFromSymbol")]
        public async Task Run([QueueTrigger("swingsellorderqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var message = JsonConvert.DeserializeObject<OrderCreationMessage>(myQueueItem);
            var userId = message.UserId;
            var symbol = message.Symbol;

            //log.LogInformation($"Function triggered from queue item to create sell orders for user {userId} for symbol {symbol} at {DateTimeOffset.Now}.");

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                log.LogError("Required data is missing from the request.");
            }

            // Connect to Blocks container
            var containerId = "Blocks";
            var container = await _repository.GetContainer(containerId);
            var blocks = new List<Block>();

            // Read user symbol block from Cosmos DB
            try
            {
                blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

                if (blocks == null)
                {
                    log.LogError($"No blocks were found for symbol {symbol}.");
                }
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting blocks from Cosmos DB item {ex.Message}.");
            }

            // Create sell orders in Alpaca if not created yet
            try
            {
                await CreateBracketOrdersBasedOnCurrentPrice(blocks, userId, symbol, container, log);
            }
            catch (Exception ex)
            {
                log.LogError($"Error creating initial sell orders: {ex.Message}.");
            }

            //log.LogInformation($"Successfully created sell orders for user {userId} symbol {symbol}.");
        }

        private async Task CreateBracketOrdersBasedOnCurrentPrice(List<Block> blocks, string userId, string symbol, Container container, ILogger log)
        {
            var currentPrice = await _order.GetCurrentPrice(_configuration, userId, symbol);

            // Get blocks above and below the current price to create sell orders
            var blocksAbove = GetBlocksAboveCurrentPriceByPercentage(blocks, currentPrice, 10);
            var blocksBelow = GetBlocksBelowCurrentPriceByPercentage(blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;

            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];
                var stopPrice = block.SellOrderPrice + (decimal)0.05;

                if (block.SellOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateStopLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, block.NumShares, stopPrice, block.SellOrderPrice, block.BuyOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial sell bracket orders for symbol {symbol} for stop price {stopPrice} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice}.");

                //ToDo: Refactor to combine with blocks below
                // Update Cosmos DB item
                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external order ids generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                // Replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(blockToUpdate, blockToUpdate.Id, new PartitionKey(userId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders.");
            }

            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];

                if (block.SellOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, block.NumShares, block.SellOrderPrice, block.BuyOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial sell bracket orders for symbol {symbol} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice}.");

                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                // replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(blockToUpdate, blockToUpdate.Id, new PartitionKey(userId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders.");
            }
        }

        private List<Block> GetBlocksAboveCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks above current price based on percentage
            var sellOrderPriceMaxAmount = currentPrice + (currentPrice * (percentage / 100));
            var blocksAbove = blocks.Where(b => b.SellOrderPrice >= currentPrice && b.SellOrderPrice <= sellOrderPriceMaxAmount).OrderBy(b => b.SellOrderPrice).ToList();
            return blocksAbove;
        }

        private List<Block> GetBlocksBelowCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks below current price based on percentage
            var sellOrderPriceMaxAmount = currentPrice - (currentPrice * (percentage / 100));
            var blocksBelow = blocks.Where(b => b.SellOrderPrice < currentPrice && b.SellOrderPrice >= sellOrderPriceMaxAmount).OrderByDescending(b => b.SellOrderPrice).ToList();
            return blocksBelow;
        }
    }
}
