using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.TradingSymbol.Models;

namespace TradingService.TradingSymbol
{
    public static class UpdateTradingSymbolOff
    {
        [FunctionName("UpdateTradingSymbolOff")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbol = JsonConvert.DeserializeObject<SymbolData>(requestBody);
            
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

            // Update symbol in Cosmos DB
            try
            {
                var tradingSymbolToUpdateResponse = await container.ReadItemAsync<SymbolData>(symbol.Id, new PartitionKey(symbol.Name));
                var tradingSymbolToUpdate = tradingSymbolToUpdateResponse.Resource;
                tradingSymbolToUpdate.Active = symbol.Active;
                tradingSymbolToUpdate.Trading = symbol.Trading;

                var updateBlockResponse = await container.ReplaceItemAsync<SymbolData>(tradingSymbolToUpdate, symbol.Id, new PartitionKey(symbol.Name));
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating Cosmos DB item {ex}", ex);
            }

            return new OkResult();
        }
    }
}
