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
using TradingService.SymbolManagement.Models;
using TradingService.SymbolManagement.Transfer;
using System.IO;
using Newtonsoft.Json;

namespace TradingService.SymbolManagement
{
    public static class CreateTradingSymbol
    {
        [FunctionName("CreateTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbolTransfer = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);
 
            if (symbolTransfer is null || string.IsNullOrEmpty(symbolTransfer.Name) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            const string containerId = "Symbols";
            var container = await Repository.GetContainer(containerId);

            var symbolToAdd = new Symbol
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                NumShares = symbolTransfer.NumShares,
                TakeProfitOffset = symbolTransfer.TakeProfitOffset,
                StopLossOffset = symbolTransfer.StopLossOffset,
                Name = symbolTransfer.Name,
                Active = symbolTransfer.Active
            };

            try
            {
                var userSymbol = await Queries.GetUserSymbolByUserId(userId);

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
                if (existingSymbols.Any(s => s.Name == symbolTransfer.Name))
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
                log.LogError($"Issue creating new symbol in Cosmos DB {ex.Message}.");
                return new BadRequestObjectResult($"Error while creating new symbol in Cosmos DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError($"Issue creating new symbol {ex.Message}.");
                return new BadRequestObjectResult($"Error while creating new symbol: {ex.Message}.");
            }
        }
    }
}
