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
using TradingService.TradingSymbol.Transfer;

namespace TradingService.TradingSymbol
{
    public static class UpdateTradingSymbol
    {
        [FunctionName("UpdateTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbol = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);

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
                var tradingSymbolToUpdateResponse = await container.ReadItemAsync<Symbol>(symbol.Id, new PartitionKey(symbol.OldName));
                var tradingSymbolToUpdate = tradingSymbolToUpdateResponse.Resource;
                tradingSymbolToUpdate.Name = symbol.Name;
                tradingSymbolToUpdate.Active = symbol.Active;
                tradingSymbolToUpdate.Trading = symbol.Trading;

                if (symbol.OldName != symbol.Name) // If changing partition key, you must delete and create a new symbol
                {
                    var deleteBlockResponse = await container.DeleteItemAsync<Symbol>(symbol.Id, new PartitionKey(symbol.OldName));
                    tradingSymbolToUpdate.Id = Guid.NewGuid().ToString();
                    tradingSymbolToUpdate.DateCreated = DateTime.Now;
                    var createNewSymbol = await container.CreateItemAsync<Symbol>(tradingSymbolToUpdate, new PartitionKey(tradingSymbolToUpdate.Name));
                }
                else
                {
                    var updateSymbolResponse = await container.ReplaceItemAsync<Symbol>(tradingSymbolToUpdate, symbol.Id, new PartitionKey(symbol.Name));
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Error updating symbol in DB {ex}", ex);
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
