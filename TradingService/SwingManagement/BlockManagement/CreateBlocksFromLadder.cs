using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradingService.SwingManagement.BlockManagement.Models;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;

namespace TradingService.SwingManagement.BlockManagement
{
    public class CreateBlocksFromLadder
    {
        private readonly IConfiguration _configuration;
        public CreateBlocksFromLadder(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CreateBlocksFromLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to create blocks.");

            var userId = req.Headers["From"].FirstOrDefault();
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladderData = JsonConvert.DeserializeObject<Ladder>(requestBody);
            decimal currentPrice;

            if (ladderData is null || string.IsNullOrEmpty(ladderData.Symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            // Connect to Blocks container in Cosmos DB
            const string databaseId = "Tracker";
            const string containerId = "Blocks";
            const string containerIdForLadders = "Ladders";
            var container = await Repository.GetContainer(databaseId, containerId);
            var containerForLadders = await Repository.GetContainer(databaseId, containerIdForLadders);

            // First check if this userSymbolBlock has already been created, if so, return a conflict result
            var existingUserSymbolBlocks = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                .Where(b => b.UserId == userId).ToList();

            if (existingUserSymbolBlocks.Any(b => b.Symbol == ladderData.Symbol)) // ToDo: See if combining this with the above improves R/U's
            {
                return new ConflictResult();
            }

            // Get current price of symbol to know where to start creating blocks from
            try
            {
                currentPrice = await Order.GetCurrentPrice(_configuration, userId, ladderData.Symbol);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting current price {ex}", ex);
                return new BadRequestObjectResult(ex.Message);
            }

            // Calculate initial num shares
            // ToDo: Use buying power to calculate percentage to get num shares
            var initialConfidenceLevel = 1;

            // Create blocks (order by buy price ascending)
            var blockPrices = GenerateBlockPrices(currentPrice, ladderData.BuyPercentage, ladderData.SellPercentage, ladderData.StopLossPercentage).OrderBy(p => p.BuyPrice);
            var blocks = new List<Block>();

            // Create a list of blocks to save based on the block prices
            var blockId = 1;
            foreach (var blockPrice in blockPrices)
            {
                var block = new Block
                {
                    Id = blockId.ToString(),
                    DateCreated = DateTime.Now,
                    ConfidenceLevel = initialConfidenceLevel,
                    BuyOrderPrice = blockPrice.BuyPrice,
                    SellOrderPrice = blockPrice.SellPrice,
                    StopLossOrderPrice = blockPrice.StopLossPrice
                };

                blocks.Add(block);
                blockId++;
            }

            try
            {
                // Create UserBlock item for user with blocks added
                var userBlockToCreate = new UserBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = ladderData.Symbol,
                    NumShares = ladderData.InitialNumShares,
                    Blocks = blocks
                };

                var newUserBlockResponse = await container.CreateItemAsync(userBlockToCreate,
                    new PartitionKey(userBlockToCreate.UserId));

                // Update ladder to indicate blocks have been created // ToDo: move this to another call after the create user block response?
                var userLadder = containerForLadders.GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(l => l.UserId == userId).ToList().FirstOrDefault();

                if (userLadder == null) return new NotFoundObjectResult("User Ladder not found.");

                var ladderToUpdate = userLadder.Ladders.FirstOrDefault(l => l.Symbol == ladderData.Symbol);
                
                if (ladderToUpdate != null)
                {
                    ladderToUpdate.BlocksCreated = true;
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
                log.LogError("Issue creating new blocks in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while creating new blocks in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue creating new blocks {ex}", ex);
                return new BadRequestObjectResult("Error while creating new blocks: " + ex);
            }
        }

        private List<BlockPrices> GenerateBlockPrices(decimal currentPrice, decimal buyPercentage, decimal sellPercentage, decimal stopLossPercentage)
        {
            var blockPrices = new List<BlockPrices>();
            const int numBlocks = 200;

            // Calculate range up
            for (var i = 0; i < numBlocks / 2; i++)
            {
                var buyPrice = currentPrice + (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var stopLossPrice = buyPrice - buyPrice * (stopLossPercentage / 100);
                var blockItemUp = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
                blockPrices.Add(blockItemUp);
            }

            // Calculate range down
            for (var i = 1; i < (numBlocks / 2); i++)
            {
                var buyPrice = currentPrice - (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var stopLossPrice = buyPrice - buyPrice * (stopLossPercentage / 100);
                var blockItemDown = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
                blockPrices.Add(blockItemDown);
            }

            return blockPrices;
        }
    }
}
