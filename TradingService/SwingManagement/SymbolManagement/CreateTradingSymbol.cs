using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using TradingService.Common.Repository;
using TradingService.SwingManagement.SymbolManagement.Models;

namespace TradingService.SwingManagement.SymbolManagement
{
    public static class CreateTradingSymbol
    {
        [FunctionName("CreateTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Symbol or user id has not been provided.");
            }

            const string databaseId = "Tracker";
            const string containerId = "Symbols";
            var container = await Repository.GetContainer(databaseId, containerId);

            var symbolToAdd = new Symbol
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Name = symbol,
                Active = true
            };

            try
            {
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbol == null) // Initial UserSymbol item creation
                {
                    // Create UserSymbol item for user with symbol added
                    var userSymbolToCreate = new UserSymbol()
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        Symbols = new List<Symbol>
                        {
                            symbolToAdd
                        }
                    };
                    var newUserSymbolResponse = await container.CreateItemAsync(userSymbolToCreate,
                        new PartitionKey(userSymbolToCreate.UserId));
                    return new OkObjectResult(newUserSymbolResponse.Resource.ToString());
                }

                // Check if symbol is added already, if so, return a conflict result
                var existingSymbols = userSymbol.Symbols.ToList();
                if (existingSymbols.Any(s => s.Name == symbol))
                {
                    return new ConflictResult();
                }

                // Add new symbol to existing UserSymbol item
                userSymbol.Symbols.Add(symbolToAdd);
                var addSymbolResponse =
                    await container.ReplaceItemAsync(userSymbol, userSymbol.Id, new PartitionKey(userSymbol.UserId));
                return new OkObjectResult(addSymbolResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating new symbol in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while creating new symbol in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue creating new symbol {ex}", ex);
                return new BadRequestObjectResult("Error while creating new symbol: " + ex);
            }
        }
    }
}
