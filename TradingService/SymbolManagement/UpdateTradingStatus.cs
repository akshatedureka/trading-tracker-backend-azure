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
    public class UpdateTradingStatus
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public UpdateTradingStatus(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
        }
        [FunctionName("UpdateTradingStatus")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var symbolTransfer = JsonConvert.DeserializeObject<SymbolTransfer>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (symbolTransfer is null || string.IsNullOrEmpty(symbolTransfer.Name))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            const string containerId = "Symbols";

            try
            {
                var container = await _repository.GetContainer(containerId);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbol == null) return new NotFoundObjectResult("User Symbol not found.");

                var symbolToUpdate = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbolTransfer.Name);

                if (symbolToUpdate != null)
                {
                    symbolToUpdate.Trading = symbolTransfer.Trading;
                }
                else
                {
                    return new NotFoundObjectResult("Symbol not found in User Symbol.");
                }

                var updateSymbolResponse = await container.ReplaceItemAsync(userSymbol, userSymbol.Id,
                        new PartitionKey(userSymbol.UserId));
                return new OkObjectResult(updateSymbolResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue removing symbol in Cosmos DB {ex.Message}.");
                return new BadRequestObjectResult($"Error while updating symbol trading status in Cosmos DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult($"Error while updating symbol trading status: {ex.Message}.");
            }
        }
    }
}
