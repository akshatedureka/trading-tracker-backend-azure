using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.BlockManagement
{
    public static class GetBlocksFromLadder
    {
        [FunctionName("GetBlocksFromLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get blocks.");

            // Get user id and symbol
            var userId = req.Headers["From"].FirstOrDefault();
            string symbol = req.Query["symbol"];

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            const string databaseId = "Tracker";
            const string containerId = "Blocks";
            var container = await Repository.GetContainer(databaseId, containerId);

            // Read blocks from Cosmos DB
            try
            {
                var userBlockResponse = container
                    .GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList().FirstOrDefault();
                return userBlockResponse != null ? new OkObjectResult(userBlockResponse.Blocks) : new OkObjectResult("No blocks found for user and symbol.");
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting blocks from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting blocks {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks:" + ex);
            }
        }
    }
}
