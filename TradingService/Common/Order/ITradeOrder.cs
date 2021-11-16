using Alpaca.Markets;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Common.Models;

namespace TradingService.Common.Order
{
    public interface ITradeOrder
    {
        public Task<Guid> CreateMarketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity);

        public Task<Guid> CreateStopLimitOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice);

        public Task<OneCancelsOtherIds> CreateOneCancelsOtherOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice);
        public Task<BracketOrderIds> CreateMarketBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<BracketOrderIds> CreateLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<BracketOrderIds> CreateStopLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<Guid> CreateTrailingStopOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal trailOffset);

        public Task<decimal> GetCurrentPrice(IConfiguration config, string userId, string symbol);

        public Task<decimal> GetPreviousDayClose(IConfiguration config, string userId, string symbol);

        public Task<bool> CancelOrderByOrderId(IConfiguration config, string userId, Guid externalOrderId);

        public Task<List<IPosition>> GetOpenPositions(IConfiguration config, string userId);

        public Task<List<IOrder>> GetOpenOrders(IConfiguration config, string userId);

        public Task<List<IPositionActionStatus>> CloseOpenPositionsAndCancelExistingOrders(IConfiguration config, string userId);

        public Task<ClosedBlock> CloseOpenPositionAndCancelExistingOrders(IConfiguration config, string userId, string symbol);

        public IAlpacaTradingClient GetAlpacaTradingClient(IConfiguration config, string userId);

        public IAlpacaDataClient GetAlpacaDataClient(IConfiguration config, string userId);
    }
}