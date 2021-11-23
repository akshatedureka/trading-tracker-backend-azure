using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Common.Models;

namespace TradingService.TradeManagement.Swing.BusinessLogic
{
    public interface ITradeManagementHelper
    {
        public List<Block> GetBlocksWithoutOpenOrders();
        public List<IOrder> GetOrdersWithoutOpenBlocks();

        public Task CreateLongBracketOrdersBasedOnCurrentPrice(UserBlock userBlock, ILogger log);

        public Task CreateShortBracketOrdersBasedOnCurrentPrice(UserBlock userBlock, ILogger log);

        public Task UpdateLongBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log);

        public Task UpdateLongSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log);

        public Task UpdateShortSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log);

        public Task UpdateShortBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log);

    }
}
