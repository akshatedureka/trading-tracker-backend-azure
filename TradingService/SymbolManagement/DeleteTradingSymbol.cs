using System;
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

namespace TradingService.SymbolManagement
{
    public class DeleteTradingSymbol
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public DeleteTradingSymbol(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
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

            const string containerId = "Symbols";

            try
            {
                var container = await _repository.GetContainer(containerId);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbol == null) return new NotFoundObjectResult("User Symbol not found.");

                userSymbol.Symbols.Remove(userSymbol.Symbols.FirstOrDefault(s => s.Name == symbol));
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
                return new BadRequestObjectResult("Error while removing symbol: " + ex);
            }
        }
    }
}
