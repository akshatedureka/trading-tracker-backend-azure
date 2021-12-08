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
using TradingService.Common.Order;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;
using TradingService.Core.Enums;

namespace TradingService.BlockManagement
{
    public class CreateBlocksFromLadder
    {
        private readonly IConfiguration _configuration;
        private readonly ITradeOrder _order;
        private readonly IBlockItemRepository _blockRepo;
        private readonly ILadderItemRepository _ladderRepo;
        private readonly IAccountItemRepository _accountRepo;

        public CreateBlocksFromLadder(IConfiguration configuration, ITradeOrder order, IBlockItemRepository blockRepo, ILadderItemRepository ladderRepo, IAccountItemRepository accountRepo)
        {
            _configuration = configuration;
            _order = order;
            _blockRepo = blockRepo;
            _ladderRepo = ladderRepo;
            _accountRepo = accountRepo;
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

            // Get account type
            var accountType = await _accountRepo.GetAccountTypeByUserId(userId);

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
                // Create a list of blocks to save based on the block prices
                foreach (var blockPrice in blockPrices)
                {
                    var block = new Block
                    {
                        UserId = userId,
                        Symbol = ladderData.Symbol,
                        NumShares = ladderData.NumSharesPerBlock,
                        ConfidenceLevel = initialConfidenceLevel,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    await _blockRepo.AddItemAsync(block);
                }

                var userLadders = await _ladderRepo.GetItemsAsyncByUserId(userId);
                var userLadder = userLadders.FirstOrDefault();

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

                var updateLadderResponse = await _ladderRepo.UpdateItemAsync(userLadder);

                return new OkObjectResult(updateLadderResponse.ToString());
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

        private List<Models.BlockPrices> GenerateBlockPrices(AccountTypes accountType, decimal currentPrice, decimal buyPercentage, decimal sellPercentage, decimal stopLossPercentage)
        {
            var blockPrices = new List<Models.BlockPrices>();
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

                var blockItemUp = new Models.BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
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

                var blockItemDown = new Models.BlockPrices { BuyPrice = buyPrice, SellPrice = sellPrice, StopLossPrice = stopLossPrice };
                blockPrices.Add(blockItemDown);
            }

            return blockPrices;
        }
    }
}
