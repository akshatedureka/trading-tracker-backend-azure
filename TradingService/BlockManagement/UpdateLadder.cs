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
using TradingService.Common.Repository;
using TradingService.SymbolManagement.Models;
using TradingService.SymbolManagement.Transfer;

namespace TradingService.BlockManagement
{
    public static class UpdateLadder
    {
        [FunctionName("UpdateLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladder = JsonConvert.DeserializeObject<Ladder>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (ladder is null || string.IsNullOrEmpty(ladder.Symbol))
            {
                return new BadRequestObjectResult("Data body is null or empty during ladder update request.");
            }

            const string databaseId = "Tracker";
            const string containerId = "Ladders";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var userLadder = container.GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(l => l.UserId == userId).ToList().FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult("User Ladder not found.");

                var ladderToUpdate = userLadder.Ladders.FirstOrDefault(l => l.Symbol == ladder.Symbol);

                if (ladderToUpdate != null)
                {
                    ladderToUpdate.InitialNumShares = ladder.InitialNumShares;
                    ladderToUpdate.BuyPercentage = ladder.BuyPercentage;
                    ladderToUpdate.SellPercentage = ladder.SellPercentage;
                    ladderToUpdate.StopLossPercentage = ladder.StopLossPercentage;
                }
                else
                {
                    return new NotFoundObjectResult("Symbol not found in User Ladder.");
                }

                var updateLadderResponse = await container.ReplaceItemAsync(userLadder, userLadder.Id,
                        new PartitionKey(userLadder.UserId));
                return new OkObjectResult(updateLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue updating ladder in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while updating ladder in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue removing symbol {ex}", ex);
                return new BadRequestObjectResult("Error while updating ladder: " + ex);
            }
        }
    }
}
