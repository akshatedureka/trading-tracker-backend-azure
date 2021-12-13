using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.Helpers.Interfaces;

namespace TradingService.Functions.SymbolManagement
{
    public class DeleteTradingSymbol
    {
        private readonly ISymbolItemRepository _symbolRepo;
        private readonly ITradingServiceHelper _tradingServiceHelper;

        public DeleteTradingSymbol(ISymbolItemRepository symbolRepo, ITradingServiceHelper tradingServiceHelper)
        {
            _symbolRepo = symbolRepo;
            _tradingServiceHelper = tradingServiceHelper;
        }

        [FunctionName("DeleteTradingSymbol")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            var symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Symbol or user id has not been provided.");
            }

            if (await _tradingServiceHelper.IsSymbolTrading(userId, symbol))
            {
                return new BadRequestObjectResult($"Symbol cannot be deleted because trading is active for symbol {symbol}.");
            }

            try
            {
                var userSymbolReponse = await _symbolRepo.GetItemsAsyncByUserId(userId);
                var userSymbol = userSymbolReponse.FirstOrDefault();

                if (userSymbol == null) return new NotFoundObjectResult("User Symbol not found.");

                userSymbol.Symbols.Remove(userSymbol.Symbols.FirstOrDefault(s => s.Name == symbol));

                var updateSymbolResponse = await _symbolRepo.UpdateItemAsync(userSymbol);

                return new OkObjectResult(updateSymbolResponse.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue removing symbol in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while removing symbol in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult("Error while removing symbol: " + ex);
            }
        }
    }
}
