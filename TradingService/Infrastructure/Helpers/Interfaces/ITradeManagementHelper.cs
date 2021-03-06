using Alpaca.Markets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Core.Entities;

namespace TradingService.Infrastructure.Helpers.Interfaces
{
    public interface ITradeManagementHelper
    {
        public List<Block> GetBlocksWithoutOpenOrders();
        public List<IOrder> GetOrdersWithoutOpenBlocks();

        public Task CreateLongBracketOrdersBasedOnCurrentPrice(List<Block> blocks, string userId, string symbol, ILogger log);

        public Task CreateShortBracketOrdersBasedOnCurrentPrice(List<Block> blocks, string userId, string symbol, ILogger log);

        public Task UpdateLongBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log);

        public Task UpdateLongSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log);

        public Task UpdateShortSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log);

        public Task UpdateShortBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log);

    }
}
