using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.BlockManagement.Models;
using TradingService.TradingSymbol.Models;
using TradingService.TradingSymbol.Transfer;

namespace TradingService.BlockManagement
{
    public static class UpdateLadder
    {
        [FunctionName("UpdateLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladder = JsonConvert.DeserializeObject<Ladder>(requestBody);

            if (ladder is null || string.IsNullOrEmpty(ladder.Symbol))
            {
                return new BadRequestObjectResult("Data body is null or empty during ladder update request.");
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

            // Update ladder in Cosmos DB
            try
            {
                var ladderToUpdateResponse =
                    await container.ReadItemAsync<Ladder>(ladder.Id, new PartitionKey(ladder.Symbol));
                var ladderToUpdate = ladderToUpdateResponse.Resource;
                ladderToUpdate.InitialNumShares = ladder.InitialNumShares;
                ladderToUpdate.BuyPercentage = ladder.BuyPercentage;
                ladderToUpdate.SellPercentage = ladder.SellPercentage;
                ladderToUpdate.StopLossPercentage = ladder.StopLossPercentage;

                var updateLadderResponse =
                    await container.ReplaceItemAsync<Ladder>(ladderToUpdate, ladder.Id,
                        new PartitionKey(ladder.Symbol));
                return new OkObjectResult(updateLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Error updating ladder in DB: {ex}", ex);
                return new BadRequestResult();
            }
        }
    }
}
