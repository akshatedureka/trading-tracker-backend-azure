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
    public static class GetTradingDataSwing
    {
        [FunctionName("GetTradingDataSwing")]
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
            var symbols = new List<Symbol>();

            // Add symbol data
            // Read symbols from Cosmos DB
            try
            {
                symbols = container.GetItemLinqQueryable<Symbol>(allowSynchronousQueryExecution: true).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
            }

            var tradingData = symbols.Select(symbol => new TradingData {SymbolId = symbol.Id, Symbol = symbol.Name, Active = symbol.Active, Trading = symbol.Trading}).ToList();

            // Add in archive data
            var blocks = new List<Block>();

            // Read block archives from Cosmos DB
            try
            {
                blocks = containerBlockArchive.GetItemLinqQueryable<Block>(allowSynchronousQueryExecution: true).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting block archives from Cosmos DB item {ex}", ex);
            }

            foreach (var tradeData in blocks.SelectMany(block => tradingData.Where(t => block.Symbol == t.Symbol)))
            {
                tradeData.ArchiveProfit = blocks.Where(b => b.Symbol == tradeData.Symbol).Sum(b => (b.ExecutedSellPrice - b.ExecutedBuyPrice) * b.NumShares);
            }

            // Add in position data
            var positions = await Order.GetOpenPositions();

            foreach (var position in positions)
            {
                foreach (var tradeData in tradingData.Where(t => position.Symbol == t.Symbol))
                {
                    tradeData.CurrentQuantity = position.Quantity;
                    tradeData.CurrentProfit = position.UnrealizedProfitLoss;
                }
            }

            // Calculate total profit
            foreach (var tradeData in tradingData)
            {
                tradeData.TotalProfit = tradeData.CurrentProfit + tradeData.ArchiveProfit;
            }

            return new OkObjectResult(JsonConvert.SerializeObject(tradingData));
        }
    }
}
