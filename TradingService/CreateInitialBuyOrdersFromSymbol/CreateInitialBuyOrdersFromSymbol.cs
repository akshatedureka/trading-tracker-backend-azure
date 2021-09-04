using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;

namespace TradingService.CreateInitialBuyOrdersFromSymbol
{
    public static class CreateInitialBuyOrdersFromSymbol
    {
        [FunctionName("CreateInitialBuyOrdersFromSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            // Get symbol name
            string symbol = req.Query["symbol"];

            // Read blocks from Cosmos DB
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri"); // ToDo: Centralize config values to common project?

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Blocks";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/symbol");

            var blocks = new List<Block>();

            // Read blocks from Cosmos DB
            try
            {
                //var iterator = container.GetItemQueryIterator<Block>(); // ToDo: centralize Block class, common project?
                using (FeedIterator<Block> setIterator = container.GetItemLinqQueryable<Block>()
                    .Where(b => b.Symbol == symbol)
                    .ToFeedIterator<Block>())
                {
                    //Asynchronous query execution
                    while (setIterator.HasMoreResults)
                    {
                        foreach (var block in await setIterator.ReadNextAsync())
                        {
                            blocks.Add(block); // ToDo: Research better way of returning all results without iterating
                        }
                    }
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating Cosmos DB item {ex}", ex);
            }

            // Create buy orders in Alpaca
            await CreateBuyLimitOrdersBasedOnCurrentPrice(blocks, symbol, container, log);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        private static async Task CreateBuyLimitOrdersBasedOnCurrentPrice(List<Block> blocks, string symbol, Container container, ILogger log)
        {
            var currentPrice = await Order.GetCurrentPrice(symbol);

            // Get blocks above and below the current price to create buy orders for
            var blocksAbove = GetBlocksAboveCurrentPriceByPercentage(blocks, currentPrice, 10);
            var blocksBelow = GetBlocksBelowCurrentPriceByPercentage(blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;
            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];
                var orderId = await Order.CreateNewOrder(OrderSide.Buy, OrderType.StopLimit, block.Symbol, block.NumShares,
                    block.BuyOrderPrice);
                //ToDo: Refactor to combine with blocks below
                // Replace Cosmos DB document
                // Get item
                var blockReplaceResponse = await container.ReadItemAsync<Block>(block.Id, new PartitionKey(symbol));
                var itemBody = blockReplaceResponse.Resource;

                // Update with external buy id generated from Alpaca
                itemBody.ExternalBuyOrderId = orderId;
                itemBody.BuyOrderCreated = true;

                // Replace the item with the updated content
                blockReplaceResponse = await container.ReplaceItemAsync<Block>(itemBody, itemBody.Id, new PartitionKey(itemBody.Symbol));
                log.LogInformation("Updated Block[{ 0},{ 1}].\n \tBody is now: { 2}\n", itemBody.ExternalBuyOrderId, itemBody.Id, blockReplaceResponse.Resource);
            }
            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];

                var orderId = await Order.CreateNewOrder(OrderSide.Buy, OrderType.Limit, block.Symbol, block.NumShares,
                    block.BuyOrderPrice);

                // Replace Cosmos DB document
                var blockReplaceResponse = await container.ReadItemAsync<Block>(block.Id, new PartitionKey(symbol));
                var itemBody = blockReplaceResponse.Resource;

                // Update with external buy id generated from Alpaca
                itemBody.ExternalBuyOrderId = orderId;
                itemBody.BuyOrderCreated = true;

                // replace the item with the updated content
                blockReplaceResponse = await container.ReplaceItemAsync<Block>(itemBody, itemBody.Id, new PartitionKey(itemBody.Symbol));
                log.LogInformation("Updated Block[{ 0},{ 1}].\n \tBody is now: { 2}\n", itemBody.ExternalBuyOrderId, itemBody.Id, blockReplaceResponse.Resource);
            }
        }

        private static List<Block> GetBlocksAboveCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks above current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice + (currentPrice * (percentage / 100));
            var blocksAbove = blocks.Where(b => b.BuyOrderPrice >= currentPrice && b.BuyOrderPrice <= buyOrderPriceMaxAmount).OrderBy(b => b.BuyOrderPrice).ToList();

            return blocksAbove;
        }

        private static List<Block> GetBlocksBelowCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks below current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice - (currentPrice * (percentage / 100));
            var blocksBelow = blocks.Where(b => b.BuyOrderPrice < currentPrice && b.BuyOrderPrice >= buyOrderPriceMaxAmount).OrderByDescending(b => b.BuyOrderPrice).ToList();

            return blocksBelow;
        }
    }
}
