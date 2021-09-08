using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using TradingService.TradingSymbol.Models;

namespace TradingService.TradingSymbol
{
    public static class CreateTradingSymbol
    {
        [FunctionName("CreateTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Get symbol name
            string symbol = req.Query["symbol"];

            if (string.IsNullOrEmpty(symbol))
            {
                return new BadRequestObjectResult("Query parameter is null or empty.");
            }

            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Symbols";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/name");

            // Create new symbol to save
            var tradingSymbol = new SymbolData()
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Name = symbol,
                Trading = false,
                Active = true
            };

            // Save block to Cosmos DB
            try
            {
                var blockResponse = await container.CreateItemAsync<SymbolData>(tradingSymbol, new PartitionKey(tradingSymbol.Name));
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating Cosmos DB item {ex}", ex);
            }

            return new OkResult();
        }
    }
}
