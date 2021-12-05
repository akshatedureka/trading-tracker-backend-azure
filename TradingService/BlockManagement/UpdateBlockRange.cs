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
    public class UpdateBlockRange
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly ITradeOrder _order;

        public UpdateBlockRange(IConfiguration configuration, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _queries = queries;
            _order = order;
        }

        [FunctionName("UpdateBlockRange")]
        public async Task Run([QueueTrigger("swingupdateblockrangequeue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var message = JsonConvert.DeserializeObject<UpdateBlockRangeMessage>(myQueueItem);
            var userId = message.UserId;
            var symbol = message.Symbol;

            // Get ladder data
            var laddersForUser = await _queries.GetLaddersByUserId(userId);
            var ladder = laddersForUser.Ladders.Where(l => l.Symbol == symbol).FirstOrDefault();

            // Get account type
            var accountType = await _queries.GetAccountTypeByUserId(userId);

            // Get current price of symbol to know where to start creating blocks from
            decimal currentPrice;
            try
            {
                currentPrice = await _order.GetCurrentPrice(_configuration, userId, symbol);
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting current price {ex.Message}.");
                throw new Exception($"Issue getting current price {ex.Message}.");
            }

            // Get current blocks
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

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
                    await _queries.DeleteBlock(block);
                }
            }

            // Remove old high blocks
            foreach (var block in blocks)
            {
                if (block.BuyOrderPrice > maxBlockPriceNew && !block.BuyOrderCreated && !block.SellOrderCreated)
                {
                    await _queries.DeleteBlock(block);
                }
            }

            // Add new low blocks
            foreach (var blockPrice in blockPrices)
            {
                if (blockPrice.BuyPrice < minBlockPriceOld)
                {
                    var block = new Block
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        Symbol = ladder.Symbol,
                        NumShares = ladder.NumSharesPerBlock,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    await _queries.CreateBlock(block);
                }
            }

            // Add new high blocks
            foreach (var blockPrice in blockPrices)
            {
                if (blockPrice.BuyPrice > maxBlockPriceOld)
                {
                    var block = new Block
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        Symbol = ladder.Symbol,
                        NumShares = ladder.NumSharesPerBlock,
                        BuyOrderPrice = blockPrice.BuyPrice,
                        SellOrderPrice = blockPrice.SellPrice,
                        StopLossOrderPrice = blockPrice.StopLossPrice
                    };

                    await _queries.CreateBlock(block);
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
