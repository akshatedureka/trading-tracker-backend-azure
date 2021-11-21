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

namespace TradingService.TradeManagement.Day
{
    public class CreateDayTradeOrders
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public CreateDayTradeOrders(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

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

            _containerSymbols = await _repository.GetContainer(containerSymbolsId);
            _containerBlocksDayArchive = await _repository.GetContainer(containerBlocksDayArchiveId);

            // Get symbols that have day trading active
            var symbols = new List<Symbol>();

            try
            {
                var userSymbolResponse = _containerSymbols
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userSymbolResponse != null) symbols = userSymbolResponse.Symbols.Where(s => s.Trading).ToList();
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
            var openOrders = await _order.GetOpenOrders(_configuration, userId);
            var openOrderSymbols = openOrders.Select(order => order.Symbol).ToList();

            // Get open positions
            var openPositions = await _order.GetOpenPositions(_configuration, userId);
            var openPositionSymbols = openPositions.Select(position => position.Symbol).ToList();

            // Loop through symbols and create buy / sell orders for previous day close price, if no order created yet and no open positions
            foreach (var symbol in symbols)
            {
                var previousDayClose = await _order.GetPreviousDayClose(_configuration, userId, symbol.Name);
                var currentPrice = await _order.GetCurrentPrice(_configuration, userId, symbol.Name);
                var longLimitPrice = previousDayClose + 0.05M;
                var shortLimitPrice = previousDayClose - 0.05M;

                //if (currentPrice > 45) continue;

                if (openOrderSymbols.Contains(symbol.Name)) continue;

                if (openPositionSymbols.Contains(symbol.Name)) continue;

                var archiveBlock = new ClosedBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = symbol.Name,
                    NumShares = 100,
                    PreviousDayClose = previousDayClose
                };

                if (currentPrice <= previousDayClose) // Go long
                {
                    try
                    {
                        // Create buy limit order for previous day close
                        var orderId = await _order.CreateStopLimitOrder(_configuration, OrderSide.Buy, userId, symbol.Name, 100, previousDayClose,
                            longLimitPrice);

                        archiveBlock.ExternalBuyOrderId = orderId;
                        archiveBlock.IsShort = false;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                        continue;
                    }

                    log.LogInformation($"Day long buy order has been created for symbol {symbol.Name} with previous day close {previousDayClose} current price {currentPrice} stop price {previousDayClose} and limit price {longLimitPrice}." );
                }
                else // Go short
                {
                    // Create sell limit order for previous day close
                    try
                    {
                        var orderId = await _order.CreateStopLimitOrder(_configuration, OrderSide.Sell, userId, symbol.Name, 100, previousDayClose,
                            shortLimitPrice);
                        archiveBlock.ExternalSellOrderId = orderId;
                        archiveBlock.IsShort = true;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                        continue;
                    }

                    log.LogInformation($"Day short sell order has been created for symbol {symbol.Name} with previous day close {previousDayClose} current price {currentPrice} stop price {previousDayClose} and limit price {shortLimitPrice}.");
                }

                await _containerBlocksDayArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
            }
        }
    }
}
