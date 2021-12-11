using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Core.Models;
using TradingService.Core.Interfaces.Persistence;

namespace TradingService.Functions.SymbolManagement
{
    public class UpdateTradingSymbol
    {
        private readonly ISymbolItemRepository _symbolRepo;

        public UpdateTradingSymbol(ISymbolItemRepository symbolRepo)
        {
            _symbolRepo = symbolRepo;
        }

        [FunctionName("UpdateTradingSymbol")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbolTransfer = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (symbolTransfer is null || string.IsNullOrEmpty(symbolTransfer.Name))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            try
            {
                var userSymbolReponse = await _symbolRepo.GetItemsAsyncByUserId(userId);
                var userSymbol = userSymbolReponse.FirstOrDefault();

                if (userSymbol == null) return new NotFoundObjectResult("User Symbol not found.");

                var symbolToUpdate = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbolTransfer.Name);

                if (symbolToUpdate != null)
                {
                    symbolToUpdate.Name = symbolTransfer.Name;
                    symbolToUpdate.Active = symbolTransfer.Active;
                    symbolToUpdate.Trading = symbolTransfer.Trading;
                }
                else
                {
                    return new NotFoundObjectResult("Symbol not found in User Symbol.");
                }

                var updateSymbolResponse = await _symbolRepo.UpdateItemAsync(userSymbol);
                return new OkObjectResult(updateSymbolResponse.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue removing symbol in Cosmos DB {ex.Message}.");
                return new BadRequestObjectResult($"Error while updating symbol in Cosmos DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult($"Error while updating symbol: {ex.Message}.");
            }
        }
    }
}
