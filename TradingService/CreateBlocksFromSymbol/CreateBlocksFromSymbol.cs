using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.CreateBlocksFromSymbol.Models;
using Microsoft.Azure.Cosmos;
using TradingService.Common.Models;
using TradingService.Common.Order;

namespace TradingService.CreateBlocksFromSymbol
{
    public static class CreateBlocksFromSymbol
    {
        [FunctionName("CreateBlocksFromSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            // Get symbol name
            string symbol = req.Query["symbol"];

            var currentPrice = await Order.GetCurrentPrice(symbol);

            // Calculate initial num shares
            // ToDo: Use buying power to calculate percentage to get num shares
            var initialNumShares = 1;
            var initialConfidenceLevel = 1;
            
            // Create blocks (order by buy price ascending)
            var blockPrices = GenerateBlockPrices(currentPrice).OrderBy(p => p.BuyPrice);

            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Blocks";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/symbol");

            // Create each block using the block prices and save to Cosmos DB
            var blockId = 1;
            foreach (var blockPrice in blockPrices)
            {
                var block = new Block
                {
                    Id = blockId.ToString(),
                    DateCreated = DateTime.Now,
                    Symbol = symbol,
                    NumShares = initialNumShares,
                    ConfidenceLevel = initialConfidenceLevel,
                    BuyOrderPrice = blockPrice.BuyPrice,
                    SellOrderPrice = blockPrice.SellPrice
                };

                // Save block to Cosmos DB
                try
                {
                    var blockResponse = await container.CreateItemAsync<Block>(block, new PartitionKey(block.Symbol));
                }
                catch (CosmosException ex)
                {
                    log.LogError("Issue creating Cosmos DB item {ex}", ex);
                }

                blockId++;
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        private static List<BlockPrices> GenerateBlockPrices(decimal currentPrice)
        {
            var blockPrices = new List<BlockPrices>();
            const int numBlocks = 200;
            const decimal buyPercentage = .15M;
            const decimal sellPercentage = .45M;

            // Calculate range up
            for (var i = 0; i < numBlocks / 2; i++)
            {
                var buyPrice = currentPrice + (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var blockItemUp = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice };
                blockPrices.Add(blockItemUp);
            }

            // Calculate range down
            for (var i = 1; i < (numBlocks / 2); i++)
            {
                var buyPrice = currentPrice - (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var blockItemDown = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice };
                blockPrices.Add(blockItemDown);
            }

            return blockPrices;
        }
    }
}
