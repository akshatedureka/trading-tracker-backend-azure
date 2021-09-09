using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using TradingService.Common.Order;
using TradingService.TradingSymbol.Models;
using TradingService.Common.Models;

namespace TradingService.TradingSymbol
{
    public static class GetTradingSymbols
    {
        [FunctionName("GetTradingSymbols")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");

            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri"); // ToDo: Centralize config values to common project?

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Symbols";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/name");

            var symbols = new List<Symbol>();

            // Read symbols from Cosmos DB
            try
            {
                symbols = container.GetItemLinqQueryable<Symbol>(allowSynchronousQueryExecution: true).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
            }

            return new OkObjectResult(JsonConvert.SerializeObject(symbols));
        }
    }
}
