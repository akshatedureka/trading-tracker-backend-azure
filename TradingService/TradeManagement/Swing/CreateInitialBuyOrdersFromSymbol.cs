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

namespace TradingService.TradeManagement.Swing
{
    public class CreateInitialBuyOrdersFromSymbol
    {
        private readonly IConfiguration _configuration;
        public CreateInitialBuyOrdersFromSymbol(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CreateInitialBuyOrdersFromSymbol")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Creating initial buy order from symbol.");

            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            // The name of the database and container we will create
            var containerId = "Blocks";

            // Connect to Cosmos DB using endpoint
            var container = await Repository.GetContainer(containerId);

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
                log.LogError("Issue getting user symbol block from Cosmos DB item {ex}", ex);
            }

            // Create buy orders in Alpaca
            try
            {
                await CreateBracketOrdersBasedOnCurrentPrice(existingUserSymbolBlock, container, log);
            }
            catch (Exception ex)
            {
                log.LogError("Error creating initial buy orders: { ex}", ex);
                return new BadRequestObjectResult("Error creating initial buy orders: " + ex);
            }

            return new OkObjectResult("Successfully created initial buy orders for symbol " + symbol);
        }

        private async Task CreateBracketOrdersBasedOnCurrentPrice(UserBlock userBlock, Container container, ILogger log)
        {
            var currentPrice = await Order.GetCurrentPrice(_configuration, userBlock.UserId, userBlock.Symbol);

            // Get blocks above and below the current price to create buy orders for
            var blocksAbove = GetBlocksAboveCurrentPriceByPercentage(userBlock.Blocks, currentPrice, 10);
            var blocksBelow = GetBlocksBelowCurrentPriceByPercentage(userBlock.Blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;

            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];
                var stopPrice = block.BuyOrderPrice - (decimal) 0.05;

                var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Buy, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, stopPrice, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
                log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", userBlock.Symbol, block.BuyOrderPrice);

                //ToDo: Refactor to combine with blocks below
                // Update Cosmos DB item
                var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = true;

                // Replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders");
            }

            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];

                var orderIds = await Order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userBlock.UserId, userBlock.Symbol, userBlock.NumShares, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
                log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", userBlock.Symbol, block.BuyOrderPrice);

                var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = true;

                // replace the item with the updated content
                var blockReplaceResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders");
            }
        }

        private List<Block> GetBlocksAboveCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks above current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice + (currentPrice * (percentage / 100));
            var blocksAbove = blocks.Where(b => b.BuyOrderPrice >= currentPrice && b.BuyOrderPrice <= buyOrderPriceMaxAmount).OrderBy(b => b.BuyOrderPrice).ToList();

            return blocksAbove;
        }

        private List<Block> GetBlocksBelowCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks below current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice - (currentPrice * (percentage / 100));
            var blocksBelow = blocks.Where(b => b.BuyOrderPrice < currentPrice && b.BuyOrderPrice >= buyOrderPriceMaxAmount).OrderByDescending(b => b.BuyOrderPrice).ToList();

            return blocksBelow;
        }
    }
}
