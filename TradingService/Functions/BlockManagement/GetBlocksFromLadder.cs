using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;
using System.Collections.Generic;

namespace TradingService.Functions.BlockManagement
{
    public class GetBlocksFromLadder
    {
        private readonly IBlockItemRepository _blockRepo;

        public GetBlocksFromLadder(IBlockItemRepository blockRepo)
        {
            _blockRepo = blockRepo;
        }

        [FunctionName("GetBlocksFromLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get blocks.");

            // Get user id and symbol
            var userId = req.Headers["From"].FirstOrDefault();
            string symbol = req.Query["symbol"];

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            // Read blocks from Cosmos DB
            try
            {
                var blocks = await _blockRepo.GetItemsAsyncByUserIdAndSymbol(userId, symbol);
                return blocks.Count != 0 ? new OkObjectResult(blocks) : new OkObjectResult(new List<Block>());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting blocks from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting blocks {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks:" + ex);
            }
        }
    }
}
