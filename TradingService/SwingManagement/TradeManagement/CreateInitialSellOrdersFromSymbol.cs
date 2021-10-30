using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;

namespace TradingService.SwingManagement.TradeManagement.Swing
{
    public class CreateInitialSellOrdersFromSymbol
    {
        private readonly IConfiguration _configuration;
        public CreateInitialSellOrdersFromSymbol(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CreateInitialSellOrdersFromSymbol")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Creating initial sell order from symbol.");

            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Blocks";

            // Connect to Cosmos DB using endpoint
            var container = await Repository.GetContainer(databaseId, containerId);

            var existingUserSymbolBlock = new UserBlock();

            // Read user symbol block from Cosmos DB
            try
            {
                existingUserSymbolBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList().FirstOrDefault();

                if (existingUserSymbolBlock == null)
                {
                    return new NotFoundObjectResult($"No blocks were found for symbol {symbol}");
                }
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting user symbol block from Cosmos DB item {ex.Message}");
                return new BadRequestObjectResult($"Issue getting user symbol block from Cosmos DB item {ex.Message}.");
            }

            // Create buy orders in Alpaca
            try
            {
                await CreateBracketOrdersBasedOnCurrentPrice(existingUserSymbolBlock, container, log);
            }
            catch (Exception ex)
            {
                log.LogError("$Error creating initial sell orders: {ex.Message}.");
                return new BadRequestObjectResult($"Error creating initial sell orders: {ex.Message}.");
            }

            return new OkObjectResult($"Successfully created initial sell orders for symbol {symbol}.");
        }

        private async Task CreateBracketOrdersBasedOnCurrentPrice(UserBlock userBlock, Container container, ILogger log)
        {
            var currentPrice = await Order.GetCurrentPrice(_configuration, userBlock.UserId, userBlock.Symbol);

            // Get blocks above and below the current price to create buy orders for
            var blocksAbove = GetBlocksAboveCurrentPriceByPercentage(userBlock.Blocks, currentPrice, 10);
            var blocksBelow = GetBlocksBelowCurrentPriceByPercentage(userBlock.Blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;

            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];
                var stopPrice = block.SellOrderPrice + (decimal) 0.05;
                var stopLossPrice = block.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down

                var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Sell, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, stopPrice, block.SellOrderPrice, block.BuyOrderPrice, stopLossPrice);
                log.LogInformation($"Created initial sell bracket orders for symbol {userBlock.Symbol} for stop price {stopPrice} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {stopLossPrice}");

                //ToDo: Refactor to combine with blocks below
                // Update Cosmos DB item
                var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external order ids generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                // Replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders");
            }

            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];
                var stopLossPrice = block.SellOrderPrice * 2; // ToDo - Update block creation to set stop loss price up instead of down

                var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, block.SellOrderPrice, block.BuyOrderPrice, stopLossPrice);
                log.LogInformation($"Created initial sell bracket orders for symbol {userBlock.Symbol} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {stopLossPrice}");

                var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                // replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders");
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
