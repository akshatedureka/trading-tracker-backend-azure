using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using TradingService.BlockManagement.Models;
using TradingService.Common.Repository;

namespace TradingService.BlockManagement
{
    public static class GetLadders
    {
        [FunctionName("GetLadders")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get ladders.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            // The name of the database and container
            const string databaseId = "Tracker";
            const string containerId = "Ladders";
            var container = await Repository.GetContainer(databaseId, containerId);

            // Read ladders from Cosmos DB
            try
            {
                var userLadderResponse = container
                    .GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();
                return userLadderResponse != null ? new OkObjectResult(userLadderResponse.Ladders) : new OkObjectResult(new List<Ladder>());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting ladders from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting ladders from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting ladders {ex}", ex);
                return new BadRequestObjectResult("Error getting ladders:" + ex);
            }
        }
    }
}
