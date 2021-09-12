using System;
using System.Linq;
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

            // Check if symbol is already created first
            // Read symbols from Cosmos DB ToDo: Create central repo for queries
            try
            {
                var existingSymbols = container.GetItemLinqQueryable<Symbol>(allowSynchronousQueryExecution: true).ToList();
                if (existingSymbols.Any(symbolToCheck => symbolToCheck.Name == symbol))
                {
                    return new ConflictResult();
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
            }

            // Create new symbol to save
            var tradingSymbol = new Symbol()
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Name = symbol,
                Active = true
            };

            // Save symbol to Cosmos DB
            try
            {
                var blockResponse = await container.CreateItemAsync<Symbol>(tradingSymbol, new PartitionKey(tradingSymbol.Name));
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating new trading symbol in DB, {ex}", ex);
            }

            return new OkObjectResult(tradingSymbol);
        }
    }
}
