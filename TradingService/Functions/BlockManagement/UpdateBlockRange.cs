using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using TradingService.Infrastructure.Services.Interfaces;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;
using TradingService.Core.Enums;
using TradingService.Core.Models;

namespace TradingService.Functions.BlockManagement
{
    public class UpdateBlockRange
    {
        private readonly IConfiguration _configuration;
        private readonly ITradeService _tradeService;
        private readonly IBlockItemRepository _blockRepo;
        private readonly ILadderItemRepository _ladderRepo;
        private readonly IAccountItemRepository _accountRepo;

        public UpdateBlockRange(IConfiguration configuration, ITradeService tradeService, IBlockItemRepository blockRepo, ILadderItemRepository ladderRepo, IAccountItemRepository accountRepo)
        {
            _configuration = configuration;
            _tradeService = tradeService;
            _blockRepo = blockRepo;
            _ladderRepo = ladderRepo;
            _accountRepo = accountRepo;
        }

        [FunctionName("UpdateBlockRange")]
        public async Task Run([QueueTrigger("updateblockrangequeue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var message = JsonConvert.DeserializeObject<UpdateBlockRangeMessage>(myQueueItem);
            var userId = message.UserId;
            var symbol = message.Symbol;

            // Get ladder data
            var userLadders = await _ladderRepo.GetItemsAsyncByUserId(userId);
            var ladder = userLadders.FirstOrDefault().Ladders.FirstOrDefault(l => l.Symbol == symbol);

            // Get account type
            var accountType = await _accountRepo.GetAccountTypeByUserId(userId);

            // Get current price of symbol to know where to start creating blocks from
            decimal currentPrice;
            try
            {
                currentPrice = await _tradeService.GetCurrentPrice(_configuration, userId, symbol);
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting current price {ex.Message}.");
                throw new Exception($"Issue getting current price {ex.Message}.");
            }

            // Get current blocks
            var blocks = await _blockRepo.GetItemsAsyncByUserIdAndSymbol(userId, symbol);

            // Get new blocks ToDo: Move this to common module
            var blockPrices = GenerateBlockPrices(accountType, currentPrice, ladder.BuyPercentage, ladder.SellPercentage, ladder.StopLossPercentage).OrderBy(p => p.BuyPrice);

            var minBlockPriceNew = blockPrices.Min(b => b.BuyPrice);
            var maxBlockPriceNew = blockPrices.Max(b => b.BuyPrice);
            var minBlockPriceOld = blocks.Min(b => b.BuyOrderPrice);
            var maxBlockPriceOld = blocks.Max(b => b.BuyOrderPrice);

            // Remove old low blocks
            foreach (var block in blocks)
            {
                if (block.BuyOrderPrice < minBlockPriceNew && !block.BuyOrderCreated && !block.SellOrderCreated)
                {
                    await _blockRepo.DeleteItemAsync(block);
                }
            }

            // Remove old high blocks
            foreach (var block in blocks)
            {
                if (block.BuyOrderPrice > maxBlockPriceNew && !block.BuyOrderCreated && !block.SellOrderCreated)
                {
                    await _blockRepo.DeleteItemAsync(block);
                }
            }

            // Add new low blocks
            foreach (var blockPrice in blockPrices)
            {
                if (blockPrice.BuyPrice < minBlockPriceOld)
                {
                    var block = new Block
                    {
                        UserId = userId,
                        Symbol = ladder.Symbol,
                        NumShares = ladder.NumSharesPerBlock,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    await _blockRepo.AddItemAsync(block);
                }
            }

            // Add new high blocks
            foreach (var blockPrice in blockPrices)
            {
                if (blockPrice.BuyPrice > maxBlockPriceOld)
                {
                    var block = new Block
                    {
                        UserId = userId,
                        Symbol = ladder.Symbol,
                        NumShares = ladder.NumSharesPerBlock,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    await _blockRepo.AddItemAsync(block);
                }
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

                if (accountType == AccountTypes.Short)
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

                if (accountType == AccountTypes.Short)
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
