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
    public static class GetTradingData
    {
        [FunctionName("GetTradingData")]
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
            var containerIdBlockArchive = "BlocksArchive";

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/name");
            var containerBlockArchive = (Container)await database.CreateContainerIfNotExistsAsync(containerIdBlockArchive, "/symbol");

            var symbols = new List<SymbolData>();

            // Read symbols from Cosmos DB
            try
            {
                using var setIterator = container.GetItemLinqQueryable<SymbolData>().ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    symbols.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
            }

            // Add in archive data
            var blocks = new List<Block>();

            // Read block archives from Cosmos DB for today only
            try
            {
                blocks = containerBlockArchive.GetItemLinqQueryable<Block>(allowSynchronousQueryExecution: true)
                    .Where(b => b.DateCreated >= DateTime.UtcNow.Date).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting block archives from Cosmos DB item {ex}", ex);
            }

            foreach (var symbol in blocks.SelectMany(block => symbols.Where(symbol => block.Symbol == symbol.Name)))
            {
                symbol.ArchiveProfit = blocks.Where(item => item.Symbol == symbol.Name).Sum(item => (item.ExecutedSellPrice - item.ExecutedBuyPrice) * item.NumShares);
            }

            // Add in position data
            var positions = await Order.GetOpenPositions();

            foreach (var position in positions)
            {
                foreach (var symbol in symbols.Where(symbol => position.Symbol == symbol.Name))
                {
                    symbol.CurrentQuantity = position.Quantity;
                    symbol.CurrentProfit = position.UnrealizedProfitLoss;
                }
            }

            // Calculate total profit
            foreach (var symbol in symbols)
            {
                symbol.TotalProfit = symbol.CurrentProfit + symbol.ArchiveProfit;
            }

            return new OkObjectResult(JsonConvert.SerializeObject(symbols));
        }
    }
}
