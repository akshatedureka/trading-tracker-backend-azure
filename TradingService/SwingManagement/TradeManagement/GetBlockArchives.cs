using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.SwingManagement.TradeManagement
{
    public static class GetBlockArchives
    {
        [FunctionName("GetBlockArchives")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get block archives.");

            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri"); // ToDo: Centralize config values to common project?

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            const string containerIdForBlockArchive = "BlocksArchive";

            var containerForBlockArchive = await Repository.GetContainer(databaseId, containerIdForBlockArchive);

            var blocks = new List<Block>();

            // Read block archives from Cosmos DB
            try
            {
                using var setIterator = containerForBlockArchive.GetItemLinqQueryable<Block>().ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    blocks.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting block archives from Cosmos DB item {ex}", ex);
            }

            return new OkObjectResult(JsonConvert.SerializeObject(blocks));
        }
    }
}
