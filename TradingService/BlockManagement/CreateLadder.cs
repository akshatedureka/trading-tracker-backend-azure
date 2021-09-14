using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.BlockManagement.Models;

namespace TradingService.BlockManagement
{
    public static class CreateLadder
    {
        [FunctionName("CreateLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladderData = JsonConvert.DeserializeObject<Ladder>(requestBody);

            if (ladderData is null || string.IsNullOrEmpty(ladderData.Symbol))
            {
                return new BadRequestObjectResult("Data body is null or empty.");
            }

            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Ladders";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/symbol");

            // Check if block header is already created first
            try
            {
                var existingLadders = container.GetItemLinqQueryable<Ladder>(allowSynchronousQueryExecution: true).ToList();
                if (existingLadders.Any(ladderToCheck => ladderToCheck.Symbol == ladderData.Symbol))
                {
                    return new ConflictResult();
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue reading existing ladders in DB {ex}", ex);
                return new BadRequestResult();
            }

            // Create new ladder to save
            var ladder = new Ladder()
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Symbol = ladderData.Symbol,
                InitialNumShares = ladderData.InitialNumShares,
                BuyPercentage = ladderData.BuyPercentage,
                SellPercentage = ladderData.SellPercentage,
                BlocksCreated = false
            };

            // Save ladder to Cosmos DB
            try
            {
                var blockHeaderResponse = await container.CreateItemAsync<Ladder>(ladder, new PartitionKey(ladder.Symbol));
                return new OkObjectResult(blockHeaderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating new ladder in DB, {ex}", ex);
                return new BadRequestResult();
            }
        }
    }
}
