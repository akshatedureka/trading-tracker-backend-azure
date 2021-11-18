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
    public class GetLadders
    {
        private readonly IQueries _queries;

        public GetLadders(IQueries queries)
        {
            _queries = queries;
        }

        [FunctionName("GetLadders")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get ladders.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            // Read ladders from Cosmos DB
            try
            {
                var userLadder = await _queries.GetLaddersByUserId(userId);
                return userLadder != null ? new OkObjectResult(userLadder.Ladders) : new OkObjectResult(new List<Ladder>());
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
