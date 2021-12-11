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
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;

namespace TradingService.Functions.LadderManagement
{
    public class UpdateLadder
    {
        private readonly ILadderItemRepository _ladderRepo;

        public UpdateLadder(ILadderItemRepository ladderRepo)
        {
            _ladderRepo = ladderRepo;
        }

        [FunctionName("UpdateLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladder = JsonConvert.DeserializeObject<Ladder>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();

            if (ladder is null || string.IsNullOrEmpty(ladder.Symbol))
            {
                return new BadRequestObjectResult("Data body is null or empty during ladder update request.");
            }

            try
            {
                var userLadderResponse = await _ladderRepo.GetItemsAsyncByUserId(userId);
                var userLadder = userLadderResponse.FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult("User Ladder not found.");

                var ladderToUpdate = userLadder.Ladders.FirstOrDefault(l => l.Symbol == ladder.Symbol);

                if (ladderToUpdate != null)
                {
                    ladderToUpdate.NumSharesPerBlock = ladder.NumSharesPerBlock;
                    ladderToUpdate.NumSharesMax = ladder.NumSharesMax;
                    ladderToUpdate.BuyPercentage = ladder.BuyPercentage;
                    ladderToUpdate.SellPercentage = ladder.SellPercentage;
                    ladderToUpdate.StopLossPercentage = ladder.StopLossPercentage;
                }
                else
                {
                    return new NotFoundObjectResult("Symbol not found in User Ladder.");
                }

                var updateLadderResponse = await _ladderRepo.UpdateItemAsync(userLadder);
                return new OkObjectResult(updateLadderResponse.ToString());
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
