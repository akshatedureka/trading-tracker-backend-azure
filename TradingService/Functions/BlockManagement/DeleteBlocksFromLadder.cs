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
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;

namespace TradingService.Functions.BlockManagement
{
    public class DeleteBlocksFromLadder
    {
        private readonly IBlockItemRepository _blockRepo;
        private readonly ILadderItemRepository _ladderRepo;
        private readonly ISymbolItemRepository _symbolRepo;

        public DeleteBlocksFromLadder(IBlockItemRepository blockRepo, ILadderItemRepository ladderRepo, ISymbolItemRepository symbolRepo)
        {
            _blockRepo = blockRepo;
            _ladderRepo = ladderRepo;
            _symbolRepo = symbolRepo;
        }

        [FunctionName("DeleteBlocksFromLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = null)] HttpRequest req,
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

            var userSymbols = await _symbolRepo.GetItemsAsyncByUserId(userId);
            var isTrading = userSymbols.FirstOrDefault().Symbols.Where(s => s.Name == ladder.Symbol).FirstOrDefault().Trading;

            if (isTrading)
            {
                return new BadRequestObjectResult($"Blocks cannot be deleted because trading is active for symbol {ladder.Symbol}.");
            }

            try
            {
                var blocks = await _blockRepo.GetItemsAsyncByUserIdAndSymbol(userId, ladder.Symbol);

                foreach (var block in blocks)
                {
                    await _blockRepo.DeleteItemAsync(block);
                }

                // Update ladder to indicate blocks have been deleted
                var userLadders = await _ladderRepo.GetItemsAsyncByUserId(userId);
                var userLadder = userLadders.FirstOrDefault();

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

                var updateLadderResponse = await _ladderRepo.UpdateItemAsync(userLadder);

                return new OkObjectResult(updateLadderResponse.ToString());
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
