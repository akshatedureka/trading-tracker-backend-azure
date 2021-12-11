using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TradingService.Infrastructure.Services.Interfaces;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;
using System.Collections.Generic;

namespace TradingService.Functions.TradeManagement
{
    public class StopTrading
    {
        private readonly IConfiguration _configuration;
        private readonly ITradeService _tradeService;
        private readonly ISymbolItemRepository _symbolRepo;
        private readonly IBlockItemRepository _blockRepo;
        private readonly IBlockClosedItemRepository _blockClosedRepo;
        private readonly IBlockCondensedItemRepository _blockCondensedRepo;

        public StopTrading(IConfiguration configuration, ITradeService tradeService, ISymbolItemRepository symbolRepo, IBlockItemRepository blockRepo, IBlockClosedItemRepository blockClosedRepo, IBlockCondensedItemRepository blockCondensedRepo)
        {
            _configuration = configuration;
            _tradeService = tradeService;
            _symbolRepo = symbolRepo;
            _blockRepo = blockRepo;
            _blockClosedRepo = blockClosedRepo;
            _blockCondensedRepo = blockCondensedRepo;
        }

        [FunctionName("StopTrading")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            log.LogInformation($"Function executed to stop trading for user {userId} and symbol {symbol}.");
                                
            try
            {
                // Turn trading off
                var userSymbols = await _symbolRepo.GetItemsAsyncByUserId(userId);
                var userSymbol = userSymbols.FirstOrDefault();

                var symbolToUpdate = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbol);
                symbolToUpdate.Trading = false;

                await _symbolRepo.UpdateItemAsync(userSymbol);

                var blocks = await _blockRepo.GetItemsAsyncByUserIdAndSymbol(userId, symbol);

                // Cancel order and close positions, return closed block information
                var closedBlock = await _tradeService.CloseOpenPositionAndCancelExistingOrders(_configuration, userId, symbol);
                if (closedBlock is null) // ToDo: split closing orders and positions. There may not be any open positions. Handle this error so that other real errors get caught and returned to the user.
                {
                    // Reset blocks
                    foreach (var block in blocks.Where(b => b.BuyOrderCreated || b.SellOrderCreated))
                    {
                        block.ExternalBuyOrderId = new Guid();
                        block.ExternalSellOrderId = new Guid();
                        block.ExternalStopLossOrderId = new Guid();
                        block.BuyOrderCreated = false;
                        block.BuyOrderFilled = false;
                        block.BuyOrderFilledPrice = 0;
                        block.DateBuyOrderFilled = DateTime.MinValue;
                        block.SellOrderCreated = false;
                        block.SellOrderFilled = false;
                        block.SellOrderFilledPrice = 0;
                        block.DateSellOrderFilled = DateTime.MinValue;

                        var updateBlock = await _blockRepo.UpdateItemAsync(block);
                    }

                    log.LogInformation("No open positions.");
                    return new OkObjectResult("There are no open positions to close.");
                }

                // Get closed blocks
                var closedBlocks = await _blockClosedRepo.GetItemsAsyncByUserIdAndSymbol(userId, symbol);

                // Move closed blocks to one condensed block
                var profit = closedBlock.Profit;
                foreach (var block in closedBlocks)
                {
                    profit += block.Profit;
                }

                var userCondensedBlocks = await _blockCondensedRepo.GetItemsAsyncByUserId(userId);
                var userCondensedBlock = userCondensedBlocks.FirstOrDefault();

                if (userCondensedBlock == null) // Initial UserCondensedBlock item creation
                {
                    var userCondensedBlockToCreate = new UserCondensedBlock()
                    {
                        UserId = userId,
                        CondensedBlocks = new List<CondensedBlock>()
                    };
                    await _blockCondensedRepo.AddItemAsync(userCondensedBlockToCreate);
                }

                var condensedBlockToUpdate = userCondensedBlock.CondensedBlocks.FirstOrDefault(l => l.Symbol == symbol);

                if (condensedBlockToUpdate == null)
                {
                    var condensedBlockToAdd = new CondensedBlock
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateUpdated = DateTime.Now,
                        Symbol = symbol,
                        Profit = profit
                    };
                    userCondensedBlock.CondensedBlocks.Add(condensedBlockToAdd);
                }
                else
                {
                    condensedBlockToUpdate.DateUpdated = DateTime.Now;
                    condensedBlockToUpdate.Profit += profit;
                }

                var updateUserCondensedBlock = await _blockCondensedRepo.UpdateItemAsync(userCondensedBlock);

                // Delete closed blocks
                foreach (var block in closedBlocks)
                {
                    await _blockClosedRepo.DeleteItemAsync(block);
                }

                // Reset blocks ToDo: Create a common query to be used throughout for resetting blocks
                foreach (var block in blocks.Where(b => b.BuyOrderCreated || b.SellOrderCreated))
                {
                    block.ExternalBuyOrderId = new Guid();
                    block.ExternalSellOrderId = new Guid();
                    block.ExternalStopLossOrderId = new Guid();
                    block.BuyOrderCreated = false;
                    block.BuyOrderFilled = false;
                    block.BuyOrderFilledPrice = 0;
                    block.DateBuyOrderFilled = DateTime.MinValue;
                    block.SellOrderCreated = false;
                    block.SellOrderFilled = false;
                    block.SellOrderFilledPrice = 0;
                    block.DateSellOrderFilled = DateTime.MinValue;

                    var updateBlock = await _blockRepo.UpdateItemAsync(block);
                }

                log.LogInformation($"Stopped trading for user {userId} and symbol {symbol} at {DateTimeOffset.Now}.");

                return new OkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
        }
    }
}
