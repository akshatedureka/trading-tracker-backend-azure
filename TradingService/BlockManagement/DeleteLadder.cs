using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using TradingService.BlockManagement.Models;
using TradingService.Common.Repository;

namespace TradingService.BlockManagement
{
    public static class DeleteLadder
    {
        [FunctionName("DeleteLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            var symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Symbol or user id has not been provided.");
            }

            const string containerId = "Ladders";

            try
            {
                var container = await Repository.GetContainer(containerId);
                var userLadder = container.GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult("User ladder not found.");

                userLadder.Ladders.Remove(userLadder.Ladders.FirstOrDefault(l => l.Symbol == symbol));
                var updateLadderResponse = await container.ReplaceItemAsync(userLadder, userLadder.Id,
                    new PartitionKey(userLadder.UserId));
                return new OkObjectResult(updateLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue removing ladder in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while removing ladder in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult("Error while removing ladder: " + ex);
            }
        }
    }
}
