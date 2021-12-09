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
using TradingService.SymbolManagement.Transfer;
using System.IO;
using Newtonsoft.Json;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;

namespace TradingService.SymbolManagement
{
    public class CreateTradingSymbol
    {
        private readonly ISymbolItemRepository _symbolRepo;

        public CreateTradingSymbol(ISymbolItemRepository symbolRepo)
        {
            _symbolRepo = symbolRepo;
        }

        [FunctionName("CreateTradingSymbol")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbolTransfer = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);
 
            if (symbolTransfer is null || string.IsNullOrEmpty(symbolTransfer.Name) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            var symbolToAdd = new Symbol
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Name = symbolTransfer.Name,
                Active = symbolTransfer.Active
            };

            try
            {
                var userSymbolReponse = await _symbolRepo.GetItemsAsyncByUserId(userId);
                var userSymbol = userSymbolReponse.FirstOrDefault();

                if (userSymbol == null) // Initial UserSymbol item creation
                {
                    // Create UserSymbol item for user with symbol added
                    var userSymbolToCreate = new UserSymbol()
                    {
                        UserId = userId,
                        Symbols = new List<Symbol>
                        {
                            symbolToAdd
                        }
                    };

                    var newUserSymbolResponse = await _symbolRepo.AddItemAsync(userSymbolToCreate);
                    return new OkObjectResult(newUserSymbolResponse.ToString());
                }

                // Check if symbol is added already, if so, return a conflict result
                var existingSymbols = userSymbol.Symbols.ToList();
                if (existingSymbols.Any(s => s.Name == symbolTransfer.Name))
                {
                    return new ConflictResult();
                }

                // Add new symbol to existing UserSymbol item
                userSymbol.Symbols.Add(symbolToAdd);
                var addSymbolResponse = await _symbolRepo.UpdateItemAsync(userSymbol);

                return new OkObjectResult(addSymbolResponse.ToString());
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
