using Alpaca.Markets;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Core.Models;
using TradingService.Core.Entities;

namespace TradingService.Infrastructure.Services.Interfaces
{
    public interface ITradeService
    {
        public Task<Guid> CreateMarketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity);

        public Task<Guid> CreateStopLimitOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice);

        public Task<OrderIds> CreateOneCancelsOtherOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice);
        public Task<OrderIds> CreateMarketBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<OrderIds> CreateLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<OrderIds> CreateStopLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice);

        public Task<Guid> CreateTrailingStopOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal trailOffset);

        public Task<decimal> GetCurrentPrice(IConfiguration config, string userId, string symbol);

        public Task<decimal> GetBuyingPower(IConfiguration config, string userId);

        public Task<decimal> GetAccountValue(IConfiguration config, string userId);

        public Task<decimal> GetAccountValuePreviousDay(IConfiguration config, string userId);

        public Task<bool> CancelOrderByOrderId(IConfiguration config, string userId, Guid externalOrderId);

        public Task<List<IPosition>> GetOpenPositions(IConfiguration config, string userId);

        public Task<List<IOrder>> GetOpenOrders(IConfiguration config, string userId);

        public Task<List<IPositionActionStatus>> CloseOpenPositionsAndCancelExistingOrders(IConfiguration config, string userId);

        public Task<ClosedBlock> CloseOpenPositionAndCancelExistingOrders(IConfiguration config, string userId, string symbol);

        public IAlpacaTradingClient GetAlpacaTradingClient(IConfiguration config, string userId);

        public IAlpacaDataClient GetAlpacaDataClient(IConfiguration config, string userId);
    }
}