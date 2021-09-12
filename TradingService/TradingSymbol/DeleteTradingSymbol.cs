using System;
using System.IO;
using System.Linq;
using System.Net;
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
    public static class DeleteTradingSymbol
    {
        [FunctionName("DeleteTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbol = JsonConvert.DeserializeObject<Symbol>(requestBody);
            
            if (symbol is null || string.IsNullOrEmpty(symbol.Name) || string.IsNullOrEmpty(symbol.Id))
            {
                return new BadRequestObjectResult("Symbol is null or empty.");
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

            // Delete symbol from Cosmos DB ToDo: Create central repo for queries
            try
            {
                var response = await container.DeleteItemAsync<Symbol>(symbol.Id, new PartitionKey(symbol.Name));
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue deleting symbol from Cosmos DB {ex}", ex);

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
