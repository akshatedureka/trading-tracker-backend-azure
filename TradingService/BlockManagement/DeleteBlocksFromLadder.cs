using System;
using System.Collections.Generic;
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
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;

namespace TradingService.BlockManagement
{
    public static class DeleteBlocksFromLadder
    {
        [FunctionName("DeleteBlocksFromLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladderData = JsonConvert.DeserializeObject<Ladder>(requestBody);
            
            if (ladderData is null || string.IsNullOrEmpty(ladderData.Symbol))
            {
                return new BadRequestObjectResult("Ladder is null or empty.");
            }

            var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

            // The name of the database and container we will create
            var databaseId = "Tracker";
            var containerId = "Blocks";
            var containerLaddersId = "Ladders";
            
            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/symbol");
            var containerLadders = (Container)await database.CreateContainerIfNotExistsAsync(containerLaddersId, "/symbol");

            // Get all blocks
            var blocks = new List<Block>();

            // Read blocks from Cosmos DB
            try
            {
                blocks = container.GetItemLinqQueryable<Block>(allowSynchronousQueryExecution: true).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting blocks to delete from DB {ex}", ex);
                return new BadRequestResult();
            }

            // Delete blocks from Cosmos DB
            try
            {
                foreach (var block in blocks.Where(block => block.Symbol == ladderData.Symbol))
                {
                    var response = await container.DeleteItemAsync<Block>(block.Id, new PartitionKey(block.Symbol));
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue deleting blocks from DB {ex}", ex);

                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return new NotFoundResult();
                }
                   
                return new BadRequestResult();
            }

            //ToDo: can be a common function?
            // Update ladder indicating blocks have already been created
            try
            {
                var ladderToUpdateResponse = await containerLadders.ReadItemAsync<Ladder>(ladderData.Id, new PartitionKey(ladderData.Symbol));
                var ladderToUpdate = ladderToUpdateResponse.Resource;
                ladderToUpdate.BlocksCreated = false;
                var updateLadderResponse = await containerLadders.ReplaceItemAsync<Ladder>(ladderToUpdate, ladderToUpdate.Id, new PartitionKey(ladderToUpdate.Symbol));
                return new OkObjectResult(updateLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Error updating ladder to indicate blocks have been created: {ex}", ex);
                return new BadRequestResult();
            }
        }
    }
}
