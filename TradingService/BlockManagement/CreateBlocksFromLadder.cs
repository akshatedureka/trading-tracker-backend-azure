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
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.AccountManagement.Models;
using TradingService.AccountManagement.Enums;

namespace TradingService.BlockManagement
{
    public class CreateBlocksFromLadder
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public CreateBlocksFromLadder(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        [FunctionName("CreateBlocksFromLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
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
            const string containerId = "Blocks";
            const string containerIdForLadders = "Ladders";
            const string containerIdForAccounts = "Accounts";
            var container = await _repository.GetContainer(containerId);
            var containerForLadders = await _repository.GetContainer(containerIdForLadders);
            var containerForAccounts = await _repository.GetContainer(containerIdForAccounts);

            // Get account type
            var accountType = await _queries.GetAccountTypeByUserId(userId);

            // Get current price of symbol to know where to start creating blocks from
            try
            {
                currentPrice = await _order.GetCurrentPrice(_configuration, userId, ladderData.Symbol);
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
            var blockPrices = GenerateBlockPrices(accountType, currentPrice, ladderData.BuyPercentage, ladderData.SellPercentage, ladderData.StopLossPercentage).OrderBy(p => p.BuyPrice);

            try
            {
                var blocks = new List<Block>();

                // Create a list of blocks to save based on the block prices
                foreach (var blockPrice in blockPrices)
                {
                    var block = new Block
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        ConfidenceLevel = initialConfidenceLevel,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    blocks.Add(block);
                }

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
                    new PartitionKey(userBlockToCreate.UserId));                // Update ladder to indicate blocks have been created // ToDo: move this to another call after the create user block response?
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

        private List<BlockPrices> GenerateBlockPrices(AccountTypes accountType, decimal currentPrice, decimal buyPercentage, decimal sellPercentage, decimal stopLossPercentage)
        {
            var blockPrices = new List<BlockPrices>();
            const int numBlocks = 200;

            // Calculate range up
            for (var i = 0; i < numBlocks / 2; i++)
            {
                var buyPrice = currentPrice + (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var stopLossPrice = buyPrice - buyPrice * (stopLossPercentage / 100);

                if (accountType == AccountTypes.SwingShort)
                {
                    stopLossPrice = sellPrice + sellPrice * (stopLossPercentage / 100);
                }

                var blockItemUp = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
                blockPrices.Add(blockItemUp);
            }

            // Calculate range down
            for (var i = 1; i < (numBlocks / 2); i++)
            {
                var buyPrice = currentPrice - (i * (buyPercentage / 100) * currentPrice);
                var sellPrice = buyPrice + buyPrice * (sellPercentage / 100);
                var stopLossPrice = buyPrice - buyPrice * (stopLossPercentage / 100);

                if (accountType == AccountTypes.SwingShort)
                {
                    stopLossPrice = sellPrice + sellPrice * (stopLossPercentage / 100);
                }

                var blockItemDown = new BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
                blockPrices.Add(blockItemDown);
            }

            return blockPrices;
        }
    }
}
