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
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.BlockManagement
{
    public class DeleteBlocksFromLadder
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public DeleteBlocksFromLadder(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
        }

        [FunctionName("DeleteBlocksFromLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
            ILogger log)
        {
            // ToDo: Delete blocks from user blocks based on user id / symbol; update ladder to indicate blocks not created
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladder = JsonConvert.DeserializeObject<Ladder>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (ladder is null || string.IsNullOrEmpty(ladder.Symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Symbol or user id has not been provided.");
            }

            try
            {
                const string containerId = "Blocks";
                const string containerIdForLadders = "Ladders";
                var container = await _repository.GetContainer(containerId);
                var containerForLadders = await _repository.GetContainer(containerIdForLadders);

                var deleteBlocks = await _queries.DeleteBlocksByUserIdAndSymbol(userId, ladder.Symbol);

                // Update ladder to indicate blocks have been deleted
                var userLadder = containerForLadders.GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(l => l.UserId == userId).ToList().FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult($"Ladder was not found for symbol {ladder.Symbol}.");

                var ladderToUpdate = userLadder.Ladders.FirstOrDefault(l => l.Symbol == ladder.Symbol);

                if (ladderToUpdate != null)
                {
                    ladderToUpdate.BlocksCreated = false;
                }
                else
                {
                    return new NotFoundObjectResult("Symbol not found in User Ladder.");
                }

                var updateLadderResponse = await containerForLadders.ReplaceItemAsync(userLadder, userLadder.Id,
                    new PartitionKey(userLadder.UserId));

                return new OkObjectResult(updateLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue deleting blocks from ladder in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while deleting blocks from ladder in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue deleting blocks from ladder {ex}", ex);
                return new BadRequestObjectResult("Error deleting blocks from ladder: " + ex);
            }
        }
    }
}
