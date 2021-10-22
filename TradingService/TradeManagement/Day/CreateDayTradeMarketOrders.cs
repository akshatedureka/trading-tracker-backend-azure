using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.AccountManagement.Enums;
using TradingService.AccountManagement.Models;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.SymbolManagement.Models;

namespace TradingService.TradeManagement.Day
{
    public class CreateDayTradeMarketOrders
    {
        private readonly IConfiguration _configuration;

        public CreateDayTradeMarketOrders(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string databaseId = "Tracker";
        private static readonly string containerSymbolsId = "Symbols";
        private static readonly string containerBlocksDayArchiveId = "BlocksDayArchive";
        private static Container _containerSymbols;
        private static Container _containerBlocksDayArchive;
        private static ILogger _log;

        [FunctionName("CreateDayTradeMarketOrders")]
        public async Task Run([QueueTrigger("daytrademarketqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _log = log;

            var account = JsonConvert.DeserializeObject<Account>(myQueueItem);
            var userId = account.UserId;
            var isShort = account.AccountType == AccountTypes.DayShort;

            _log.LogInformation($"Function triggered to process day trade market orders for user {userId}.");

            _containerSymbols = await Repository.GetContainer(databaseId, containerSymbolsId);
            _containerBlocksDayArchive = await Repository.GetContainer(databaseId, containerBlocksDayArchiveId);

            // Get symbols that have day trading active
            var symbols = new List<Symbol>();

            try
            {
                var userSymbolResponse = _containerSymbols
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbolResponse != null) symbols = userSymbolResponse.Symbols.Where(s => s.DayTrading).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting symbols {ex}", ex);
            }

            // Get open orders
            var openOrders = await Order.GetOpenOrders(_configuration, userId);
            var openOrderSymbols = openOrders.Select(order => order.Symbol).ToList();

            // Get open positions
            var openPositions = await Order.GetOpenPositions(_configuration, userId);
            var openPositionSymbols = openPositions.Select(position => position.Symbol).ToList();

            // Loop through symbols and create buy / sell orders for previous day close price, if no order created yet and no open positions
            foreach (var symbol in symbols)
            {
                var currentPrice = await Order.GetCurrentPrice(_configuration, userId, symbol.Name);

                //if (currentPrice > 45) continue;

                if (openOrderSymbols.Contains(symbol.Name)) continue;

                if (openPositionSymbols.Contains(symbol.Name)) continue;

                var archiveBlock = new ArchiveBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = symbol.Name,
                    NumShares = 100,
                    CurrentPrice = currentPrice
                };

                if (!isShort) // Go long
                {
                    try
                    {
                        // Create buy market order
                        var orderId = await Order.CreateMarketOrder(_configuration, OrderSide.Buy, userId, symbol.Name, 100);
                        
                        archiveBlock.ExternalBuyOrderId = orderId;
                        archiveBlock.IsShort = false;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                        continue;
                    }

                    log.LogInformation($"Day long market order has been created for symbol {symbol.Name} with current price {currentPrice}." );
                }
                else // Go short
                {
                    // Create sell market order
                    try
                    {
                        var orderId = await Order.CreateMarketOrder(_configuration, OrderSide.Sell, userId, symbol.Name, 100);

                        archiveBlock.ExternalSellOrderId = orderId;
                        archiveBlock.IsShort = true;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                        continue;
                    }

                    log.LogInformation($"Day short market order has been created for symbol {symbol.Name} with current price {currentPrice}.");
                }

                await _containerBlocksDayArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
            }
        }
    }
}
