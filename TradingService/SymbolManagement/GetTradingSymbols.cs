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
using TradingService.Common.Repository;
using TradingService.SymbolManagement.Models;

namespace TradingService.SymbolManagement
{
    public static class GetTradingSymbols
    {
        [FunctionName("GetTradingSymbols")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.User, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            // The name of the database and container
            const string databaseId = "Tracker";
            const string containerId = "Symbols";
            var container = await Repository.GetContainer(databaseId, containerId);

            // Read symbols from Cosmos DB
            try
            {
                var userSymbolResponse = container
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();
                return userSymbolResponse != null ? new OkObjectResult(userSymbolResponse.Symbols) : new OkObjectResult(new List<Symbol>());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting symbols from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting symbols {ex}", ex);
                return new BadRequestObjectResult("Error getting symbols:" + ex);
            }
        }
    }
}
