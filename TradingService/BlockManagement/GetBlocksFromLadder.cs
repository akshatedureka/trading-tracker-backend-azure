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
using Microsoft.Azure.Cosmos;
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;

namespace TradingService.BlockManagement
{
    public static class GetBlocksFromLadder
    {
        [FunctionName("GetBlocksFromLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get blocks.");

            // Get symbol
            string symbol = req.Query["symbol"];

            if (string.IsNullOrEmpty(symbol))
            {
                return new BadRequestObjectResult("Query parameter is null or empty.");
            }

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
                blocks = container.GetItemLinqQueryable<Block>(allowSynchronousQueryExecution: true)
                    .Where(b => b.Symbol == symbol).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting blocks from DB {ex}", ex);
                return new BadRequestResult();
            }

            return new OkObjectResult(JsonConvert.SerializeObject(blocks));
        }
    }
}
