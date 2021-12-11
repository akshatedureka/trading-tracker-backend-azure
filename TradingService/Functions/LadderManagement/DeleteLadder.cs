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

namespace TradingService.Functions.LadderManagement
{
    public class DeleteLadder
    {
        private readonly ILadderItemRepository _ladderRepo;

        public DeleteLadder(ILadderItemRepository ladderRepo)
        {
            _ladderRepo = ladderRepo;
        }

        [FunctionName("DeleteLadder")]
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

            try
            {
                var userLadderResponse = await _ladderRepo.GetItemsAsyncByUserId(userId);
                var userLadder = userLadderResponse.FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult("User ladder not found.");

                userLadder.Ladders.Remove(userLadder.Ladders.FirstOrDefault(l => l.Symbol == symbol));
                var updateLadderResponse = await _ladderRepo.UpdateItemAsync(userLadder);

                return new OkObjectResult(updateLadderResponse.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue removing ladder in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while removing ladder in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult("Error while removing ladder: " + ex);
            }
        }
    }
}
