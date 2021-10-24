using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.DayManagement.TradeManagement.Models;

namespace TradingService.DayManagement.TradeManagement.Day
{
    public class UpdateDayMarketOrderFromQueueMsg
    {
        private readonly IConfiguration _configuration;

        public UpdateDayMarketOrderFromQueueMsg(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private static readonly string databaseId = "Tracker";
        private static readonly string containerBlocksDayArchiveId = "BlocksDayArchive";
        private static Container _containerBlocksDayArchive;
        private static ILogger _log;

        [FunctionName("UpdateDayMarketOrderFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeuedaymarket", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _containerBlocksDayArchive = await Repository.GetContainer(databaseId, containerBlocksDayArchiveId);

            _log = log;

            var orderUpdateMessage = JsonConvert.DeserializeObject<OrderUpdateMessage>(myQueueItem);

            if (orderUpdateMessage.OrderSide == OrderSide.Buy)
            {
                await UpdateBuyOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }
            else
            {
                await UpdateSellOrderExecuted(orderUpdateMessage.UserId, orderUpdateMessage.Symbol, orderUpdateMessage.OrderId, orderUpdateMessage.ExecutedPrice);
            }

            _log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

        }

        private async Task UpdateBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice)
        {
            // Buy order has been executed, update block to record buy order has been filled
            _log.LogInformation($"Buy order executed for user id {userId}, symbol {symbol}, executedBuyPrice {executedBuyPrice}, external order id {externalOrderId} at: {DateTimeOffset.Now}");

            // Get day trade block
            var dayBlock = await GetArchiveDayBlockIfExists(userId, externalOrderId);

            if (dayBlock != null)
            {
                if (!dayBlock.IsShort)
                {
                    try
                    {
                        var orderIds = await Order.CreateOneCancelsOtherOrder(_configuration, OrderSide.Sell, userId, symbol,
                            dayBlock.NumShares, executedBuyPrice + .10M, executedBuyPrice - .05M);
                        dayBlock.ExternalSellOrderId = orderIds.TakeProfitId;
                        dayBlock.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"Failure creating long buy order: {ex.Message}");
                    }
                }
                else
                {
                    dayBlock.Profit = (dayBlock.SellOrderFilledPrice - dayBlock.BuyOrderFilledPrice) * dayBlock.NumShares;
                }

                // Update day block with buy order executed and external sell order id
                dayBlock.DateBuyOrderFilled = DateTime.Now;
                dayBlock.BuyOrderFilledPrice = executedBuyPrice;

                var dayBlockReplaceResponse = await _containerBlocksDayArchive.ReplaceItemAsync(dayBlock, dayBlock.Id, new PartitionKey(dayBlock.UserId));
                _log.LogInformation($"Day block has been updated for buy for short {dayBlock.IsShort} user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}");
            }
            else
            {
                _log.LogError($"Day block not found for buy user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}");
            }
        }

        private async Task UpdateSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice)
        {
            // Sell order has been executed, create new buy order in Alpaca, archive and reset block
            _log.LogInformation($"Sell order executed for user id {userId}, symbol {symbol}, executed sell price {executedSellPrice}, external order id {externalOrderId} at: {DateTimeOffset.Now}");

            // Get day block
            var dayBlock = await GetArchiveDayBlockIfExists(userId, externalOrderId);

            if (dayBlock != null)
            {
                if (dayBlock.IsShort)
                {
                    try
                    {
                        var orderIds = await Order.CreateOneCancelsOtherOrder(_configuration, OrderSide.Buy, userId, symbol,
                            dayBlock.NumShares, executedSellPrice - .10M, executedSellPrice + .05M);
                        dayBlock.ExternalBuyOrderId = orderIds.TakeProfitId;
                        dayBlock.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"Failure creating short sell order: {ex.Message}");
                    }
                }
                else
                {
                    dayBlock.Profit = (dayBlock.SellOrderFilledPrice - dayBlock.BuyOrderFilledPrice) * dayBlock.NumShares;
                }

                // Update day block
                dayBlock.DateSellOrderFilled = DateTime.Now;
                dayBlock.SellOrderFilledPrice = executedSellPrice;

                var dayBlockReplaceResponse = await _containerBlocksDayArchive.ReplaceItemAsync(dayBlock, dayBlock.Id, new PartitionKey(dayBlock.UserId));
                _log.LogInformation($"Day block has been updated for sell for short {dayBlock.IsShort} user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}");
            }
            else
            {
                _log.LogError($"Day block not found for sell user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}");
            }
        }

        private async Task<ArchiveBlock> GetArchiveDayBlockIfExists(string userId, Guid externalOrderId)
        {
            // Read user blocks from Cosmos DB
            var archiveDayBlock = new List<ArchiveBlock>();
            try
            {
                using var setIterator = _containerBlocksDayArchive.GetItemLinqQueryable<ArchiveBlock>()
                    .Where(b => b.UserId == userId && (b.ExternalBuyOrderId == externalOrderId || b.ExternalSellOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId))
                    .ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    archiveDayBlock.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                _log.LogError($"Issue getting archive day block from Cosmos DB item {ex.Message}");
            }

            return archiveDayBlock.FirstOrDefault();
        }
    }
}