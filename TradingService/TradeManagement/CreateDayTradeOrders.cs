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
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.SymbolManagement.Models;
using TradingService.TradeManagement.Models;

namespace TradingService.TradeManagement
{
    public class CreateDayTradeOrders
    {
        private readonly IConfiguration _configuration;

        public CreateDayTradeOrders(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string databaseId = "Tracker";
        private static readonly string containerSymbolsId = "Symbols";
        private static readonly string containerBlocksDayArchiveId = "BlocksDayArchive";
        private static Container _containerSymbols;
        private static Container _containerBlocksDayArchive;
        private static ILogger _log;

        [FunctionName("CreateDayTradeOrders")]
        public async Task Run([QueueTrigger("daytradequeue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _log = log;

            var userId = JsonConvert.DeserializeObject<string>(myQueueItem);

            _log.LogInformation($"Function triggered to process day trade orders for user {userId}.");

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

            // Loop through symbols and create buy orders for previous day close price, if no order created yet
            foreach (var symbol in symbols)
            {
                var previousDayClose = await Order.GetPreviousDayClose(_configuration, userId, symbol.Name);
                var currentPrice = await Order.GetCurrentPrice(_configuration, userId, symbol.Name);
                var limitPrice = previousDayClose + 0.05M;

                if (currentPrice >= previousDayClose || openOrderSymbols.Contains(symbol.Name)) continue;

                // Create buy limit order for previous day close
                var orderId = await Order.CreateStopLimitOrder(_configuration, OrderSide.Buy, userId, symbol.Name, 100, previousDayClose,
                    limitPrice);

                // Save order to archive day block table
                var archiveBlock = new ArchiveBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = symbol.Name,
                    NumShares = 100,
                    ExternalBuyOrderId = orderId,
                };

                await _containerBlocksDayArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
            }
        }
    }
}
