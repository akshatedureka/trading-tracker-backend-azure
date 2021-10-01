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
using TradingService.SymbolManagement.Models;
using TradingService.SymbolManagement.Transfer;
using TradingService.Common.Repository;

namespace TradingService.SymbolManagement
{
    public static class UpdateTradingSymbol
    {
        [FunctionName("UpdateTradingSymbol")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbol = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (symbol is null || string.IsNullOrEmpty(symbol.Name))
            {
                return new BadRequestObjectResult("Data body is null or empty during symbol update request.");
            }

            var symbolNameToUpdate = symbol.OldName ?? symbol.Name;

            const string databaseId = "Tracker";
            const string containerId = "Symbols";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbol == null) return new NotFoundObjectResult("User Symbol not found");

                foreach (var symbolToUpdate in userSymbol.Symbols.Where(symbolToUpdate => symbolToUpdate.Name == symbolNameToUpdate))
                {
                    symbolToUpdate.Name = symbol.Name;
                    symbolToUpdate.Active = symbol.Active;
                    symbolToUpdate.Trading = symbol.Trading;
                }

                var updateSymbolResponse = await container.ReplaceItemAsync(userSymbol, userSymbol.Id,
                        new PartitionKey(userSymbol.UserId));
                    return new OkObjectResult(updateSymbolResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue removing symbol in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while removing symbol in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult("Error while removing new symbol: " + ex);
            }
        }
    }
}
