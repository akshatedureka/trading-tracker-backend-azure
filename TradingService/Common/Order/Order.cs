using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alpaca.Markets;
using TradingService.Common.Models;

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
                case OrderType.Market:
                    try
                    {
                        var order = await AlpacaTradingClient.PostOrderAsync(orderSide.Market(symbol, numShares).WithDuration(TimeInForce.Gtc));
                        return order.OrderId;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error while creating Market order in Alpaca {e}: ", e);
                        throw;
                    }
                default:
                    break;
            }

            return new Guid();
        }

        public static async Task<Guid> CreateBracketOrder(OrderSide orderSide, string symbol, long quantity, decimal stopPrice, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice)
        {
            //var order = await AlpacaTradingClient.PostOrderAsync(orderSide.StopLimit(symbol, quantity, stopPrice, limitPrice).TakeProfit(takeProfitPrice));
            var bracketOrder = await AlpacaTradingClient.PostOrderAsync(orderSide.StopLimit(symbol, quantity, stopPrice, limitPrice).Bracket(takeProfitPrice, stopLossPrice));
            return bracketOrder.OrderId;
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

        public static async Task<bool> CancelOrder(Guid externalOrderId)
        {
            try
            {
                var isOrderCanceled = await AlpacaTradingClient.DeleteOrderAsync(externalOrderId);
                return isOrderCanceled;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling order in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<List<IPosition>> GetOpenPositions()
        {
            try
            {
                var positions = await AlpacaTradingClient.ListPositionsAsync();
                return positions.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling order in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<List<IPositionActionStatus>> CloseOpenPositionsAndCancelExistingOrders()
        {
            // Delete all open positions, cancels all open orders before liquidating
            try
            {
                var result = await AlpacaTradingClient.DeleteAllPositionsAsync(new DeleteAllPositionsRequest { CancelOrders = true });
                var positionStatus = result.ToList();
                //positionStatus[0].Symbol;
                //positionStatus[0].IsSuccess;
                // ToDo: Return list of blocks to archive
                return positionStatus;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling order in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<Block> CloseOpenPositionAndCancelExistingOrders(string symbol)
        {
            try
            {
                // Cancel open orders for symbol
                var orders = await AlpacaTradingClient.ListOrdersAsync(new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open }.WithSymbol(symbol));
                foreach (var order in orders)
                {
                    var orderCancelResult = await AlpacaTradingClient.DeleteOrderAsync(order.OrderId);
                }

                // Wait for orders to cancel before proceeding
                var waitingForOrdersToCancel = true;
                while (waitingForOrdersToCancel)
                {
                    Task.Delay(1000).Wait();
                    orders = await AlpacaTradingClient.ListOrdersAsync(new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open }.WithSymbol(symbol));
                    var numOrders = orders.ToList().Count;
                    if (numOrders == 0)
                    {
                        waitingForOrdersToCancel = false;
                    }
                    Console.WriteLine($"Waiting for orders to cancel in Alpaca. Count is {numOrders}: ", numOrders);
                }

                // Delete open position for symbol
                var positionData = await AlpacaTradingClient.GetPositionAsync(symbol);
                var result = await AlpacaTradingClient.DeletePositionAsync(new DeletePositionRequest(symbol));

                // Return a block to be archived (assume it sells since it is a market order - could create a new block for the order and wait for it to sell if it becomes an issue)
                var block = new Block
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = result.Symbol,
                    NumShares = result.IntegerQuantity,
                    DateCreated = DateTime.Now,
                    ExecutedBuyPrice = positionData.AverageEntryPrice,
                    ExecutedSellPrice = positionData.AssetCurrentPrice,
                    ExternalSellOrderId = result.OrderId,
                    ExternalBuyOrderId = Guid.NewGuid(),
                };

                return block;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling orders / closing positions in Alpaca {e}: ", e);
                return null;
            }
        }
    }
}
