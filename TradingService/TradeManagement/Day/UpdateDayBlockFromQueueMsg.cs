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
using TradingService.TradeManagement.Day.Models;

namespace TradingService.TradeManagement.Day
{
    public class UpdateDayBlockFromQueueMsg
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public UpdateDayBlockFromQueueMsg(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        private static readonly string containerBlocksDayArchiveId = "BlocksDayArchive";
        private static Container _containerBlocksDayArchive;
        private static ILogger _log;

        [FunctionName("UpdateDayBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("tradeupdatequeueday", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            _containerBlocksDayArchive = await _repository.GetContainer(containerBlocksDayArchiveId);

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
            _log.LogInformation($"Buy order executed for trading block for user id {userId}, symbol {symbol}, executedBuyPrice {executedBuyPrice}, external order id {externalOrderId} at: {DateTimeOffset.Now}");

            // Get day trade block
            var dayBlock = await GetArchiveDayBlockIfExists(userId, externalOrderId);

            if (dayBlock != null)
            {
                if (!dayBlock.IsShort)
                {
                    try
                    {
                        // ToDo: trailing stop must be more than .001 of executed buy price, if .05 is less than required amount use .0015 of buy price
                        var orderId = await _order.CreateTrailingStopOrder(_configuration, OrderSide.Sell, userId, symbol,
                            dayBlock.NumShares, .5M);
                        dayBlock.ExternalSellOrderId = orderId;
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
            _log.LogInformation($"Sell order executed for trading block for user id {userId}, symbol {symbol}, executed sell price {executedSellPrice}, external order id {externalOrderId} at: {DateTimeOffset.Now}");

            // Get day block
            var dayBlock = await GetArchiveDayBlockIfExists(userId, externalOrderId);

            if (dayBlock != null)
            {
                if (dayBlock.IsShort)
                {
                    try
                    {
                        // ToDo: trailing stop must be more than .001 of executed buy price, if .05 is less than required amount use .0015 of buy price
                        var orderId = await _order.CreateTrailingStopOrder(_configuration, OrderSide.Buy, userId, symbol,
                            dayBlock.NumShares, .5M);
                        dayBlock.ExternalBuyOrderId = orderId;
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

        private async Task<ClosedBlock> GetArchiveDayBlockIfExists(string userId, Guid externalOrderId)
        {
            // Read user blocks from Cosmos DB
            var archiveDayBlock = new List<ClosedBlock>();
            try
            {
                using var setIterator = _containerBlocksDayArchive.GetItemLinqQueryable<ClosedBlock>()
                    .Where(b => b.UserId == userId && (b.ExternalBuyOrderId == externalOrderId || b.ExternalSellOrderId == externalOrderId))
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
