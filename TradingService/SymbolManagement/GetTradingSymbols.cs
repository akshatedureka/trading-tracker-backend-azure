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
using TradingService.SymbolManagement.Models;
using TradingService.Core.Interfaces.Persistence;

namespace TradingService.SymbolManagement
{
    public class GetTradingSymbols
    {
        private readonly ISymbolItemRepository _symbolRepo;

        public GetTradingSymbols(ISymbolItemRepository symbolRepo)
        {
            _symbolRepo = symbolRepo;
        }

        [FunctionName("GetTradingSymbols")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            // Read symbols from Cosmos DB
            try
            {
                var userSymbolResponse = await _symbolRepo.GetItemsAsyncByUserId(userId);  

                return userSymbolResponse.Count != 0 ? new OkObjectResult(userSymbolResponse.FirstOrDefault().Symbols) : new OkObjectResult(new List<Symbol>());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex.Message);
                return new BadRequestObjectResult("Error getting symbols from DB: " + ex.Message);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting symbols {ex}", ex.Message);
                return new BadRequestObjectResult("Error getting symbols:" + ex.Message);
            }
        }
    }
}
