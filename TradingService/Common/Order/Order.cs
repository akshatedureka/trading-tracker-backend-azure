using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Alpaca.Markets;

namespace TradingService.Common.Order
{
    public static class Order
    {
        private static readonly string AlpacaAPIKey = Environment.GetEnvironmentVariable("AlpacaPaperAPIKey");
        private static readonly string AlpacaAPISecret = Environment.GetEnvironmentVariable("AlpacaPaperAPISecret");
        private static readonly IAlpacaTradingClient AlpacaTradingClient =
            Environments.Paper.GetAlpacaTradingClient(new SecretKey(AlpacaAPIKey, AlpacaAPISecret));
        private static readonly IAlpacaDataClient AlpacaDataClient =
            Environments.Paper.GetAlpacaDataClient(new SecretKey(AlpacaAPIKey, AlpacaAPISecret));

        public static async Task<Guid> CreateNewOrder(OrderSide orderSide, OrderType orderType, string symbol, long numShares,
            decimal orderPrice)
        {
            var StopLimitOrderOffset = 0.01M; // ToDo research percentage based? currentPrice/1000?
            switch (orderType)
            {
                case OrderType.Limit:
                    try
                    {
                        var order = await AlpacaTradingClient.PostOrderAsync(orderSide
                            .Limit(symbol, numShares, orderPrice).WithDuration(TimeInForce.Gtc));
                        return order.OrderId;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error while creating Limit order in Alpaca {e}: ", e);
                        throw;
                    }
                case OrderType.StopLimit:
                    try
                    {
                        var order = await AlpacaTradingClient.PostOrderAsync(orderSide.StopLimit(symbol, numShares, orderPrice - StopLimitOrderOffset, orderPrice).WithDuration(TimeInForce.Gtc));
                        return order.OrderId;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error while creating StopLimit order in Alpaca {e}: ", e);
                        throw;
                    }

                default:
                    break;
            }

            return new Guid();
        }

        public static async Task<decimal> GetCurrentPrice(string symbol)
        {
            try
            {
                var latestTrade = await AlpacaDataClient.GetLatestTradeAsync(symbol);
                return latestTrade.Price;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
