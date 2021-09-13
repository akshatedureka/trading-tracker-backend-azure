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

namespace TradingService.BlockManagement
{
    public static class DeleteLadder
    {
        [FunctionName("DeleteLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladder = JsonConvert.DeserializeObject<Ladder>(requestBody);
            
            if (ladder is null || string.IsNullOrEmpty(ladder.Symbol) || string.IsNullOrEmpty(ladder.Id))
            {
                return new BadRequestObjectResult("Ladder is null or empty.");
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

            // Delete ladder from Cosmos DB
            try
            {
                var response = await container.DeleteItemAsync<Ladder>(ladder.Id, new PartitionKey(ladder.Symbol));
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue deleting ladder from Cosmos DB {ex}", ex);

                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return new NotFoundResult();
                }
                   
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
