using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.CreateBlocksFromSymbol.Models;
using Microsoft.Azure.Cosmos;
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;
using TradingService.Common.Order;

namespace TradingService.BlockManagement
{
    public static class CreateBlocksFromLadder
    {
        [FunctionName("CreateBlocksFromLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to create blocks.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladderData = JsonConvert.DeserializeObject<Ladder>(requestBody);

            if (ladderData is null || string.IsNullOrEmpty(ladderData.Symbol))
            {
                return new BadRequestObjectResult("Data body is null or empty.");
            }

            var currentPrice = await Order.GetCurrentPrice(ladderData.Symbol);

            // Calculate initial num shares
            // ToDo: Use buying power to calculate percentage to get num shares
            var initialConfidenceLevel = 1;
            
            // Create blocks (order by buy price ascending)
            var blockPrices = GenerateBlockPrices(currentPrice, ladderData.BuyPercentage, ladderData.SellPercentage).OrderBy(p => p.BuyPrice);

            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "NewBlocks";
            var containerLaddersId = "Ladders";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/symbol");
            var containerLadders = (Container)await database.CreateContainerIfNotExistsAsync(containerLaddersId, "/symbol");

            // Create each block using the block prices and save to Cosmos DB
            var blockId = 1;
            foreach (var blockPrice in blockPrices)
            {
                var block = new Block
                {
                    Id = blockId.ToString(),
                    DateCreated = DateTime.Now,
                    Symbol = ladderData.Symbol,
                    NumShares = ladderData.InitialNumShares,
                    ConfidenceLevel = initialConfidenceLevel,
                    BuyOrderPrice = blockPrice.BuyPrice,
                    SellOrderPrice = blockPrice.SellPrice
                };

                // Save blocks to Cosmos DB
                try
                {
                    var blockResponse = await container.CreateItemAsync<Block>(block, new PartitionKey(block.Symbol));
                }
                catch (CosmosException ex)
                {
                    log.LogError("Issue creating blocks {ex}", ex);
                    return new BadRequestResult();
                }

                blockId++;
            }

            // Update ladder indicating blocks have been created
            try
            {
                var ladderToUpdateResponse = await containerLadders.ReadItemAsync<Ladder>(ladderData.Id, new PartitionKey(ladderData.Symbol));
                var ladderToUpdate = ladderToUpdateResponse.Resource;
                ladderToUpdate.BlocksCreated = true;
                var updateLadderResponse = await containerLadders.ReplaceItemAsync<Ladder>(ladderToUpdate, ladderToUpdate.Id, new PartitionKey(ladderToUpdate.Symbol));
            }
            catch (CosmosException ex)
            {
                log.LogError("Error updating ladder to indicate blocks have been created: {ex}", ex);
                return new BadRequestResult();
            }

            return new OkResult();
        }

        private static List<BlockPrices> GenerateBlockPrices(decimal currentPrice, decimal buyPercentage, decimal sellPercentage)
        {
            var blockPrices = new List<BlockPrices>();
            const int numBlocks = 200;

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
