using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Extensions.Configuration;
using TradingService.AccountManagement.Enums;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.Common.Order
{
    public static class Order
    {
        public static async Task<Guid> CreateMarketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var stopLimitOrder = await alpacaTradingClient.PostOrderAsync(orderSide
                .Market(symbol, quantity));

            return stopLimitOrder.OrderId;
        }

        public static async Task<Guid> CreateStopLimitOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var stopLimitOrder = await alpacaTradingClient.PostOrderAsync(orderSide
                .StopLimit(symbol, quantity, stopPrice, limitPrice));

            return stopLimitOrder.OrderId;
        }

        public static async Task<OneCancelsOtherIds> CreateOneCancelsOtherOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var ocoOrder = await alpacaTradingClient.PostOrderAsync(orderSide.Limit(symbol, quantity, takeProfitPrice).OneCancelsOther(stopLossPrice));
            
            return new OneCancelsOtherIds()
            {
                TakeProfitId = ocoOrder.OrderId,
                StopLossOrderId =
                    ocoOrder.Legs.FirstOrDefault(l => l.OrderType == OrderType.Stop).OrderId
            };
        }

        public static async Task<BracketOrderIds> CreateMarketBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal takeProfitPrice, decimal stopLossPrice)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var bracketOrder = await alpacaTradingClient.PostOrderAsync(orderSide.Market(symbol, quantity).Bracket(takeProfitPrice, stopLossPrice));

            return new BracketOrderIds
            {
                ParentOrderId = bracketOrder.OrderId,
                TakeProfitId = bracketOrder.Legs.Where(l => l.OrderType == OrderType.Limit).FirstOrDefault().OrderId,
                StopLossOrderId =
                    bracketOrder.Legs.Where(l => l.OrderType == OrderType.Stop).FirstOrDefault().OrderId
            };
        }

        public static async Task<BracketOrderIds> CreateLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var bracketOrder = await alpacaTradingClient.PostOrderAsync(orderSide.Limit(symbol, quantity, limitPrice).Bracket(takeProfitPrice, stopLossPrice));

            return new BracketOrderIds
            {
                ParentOrderId = bracketOrder.OrderId,
                TakeProfitId = bracketOrder.Legs.Where(l => l.OrderType == OrderType.Limit).FirstOrDefault().OrderId,
                StopLossOrderId =
                    bracketOrder.Legs.Where(l => l.OrderType == OrderType.Stop).FirstOrDefault().OrderId
            };
        }

        public static async Task<BracketOrderIds> CreateStopLimitBracketOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal stopPrice, decimal limitPrice, decimal takeProfitPrice, decimal stopLossPrice)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var bracketOrder = await alpacaTradingClient.PostOrderAsync(orderSide.StopLimit(symbol, quantity, stopPrice, limitPrice).Bracket(takeProfitPrice, stopLossPrice));

            return new BracketOrderIds
            {
                ParentOrderId = bracketOrder.OrderId,
                TakeProfitId = bracketOrder.Legs.Where(l => l.OrderType == OrderType.Limit).FirstOrDefault().OrderId,
                StopLossOrderId =
                    bracketOrder.Legs.Where(l => l.OrderType == OrderType.Stop).FirstOrDefault().OrderId
            };
        }
        public static async Task<Guid> CreateTrailingStopOrder(IConfiguration config, OrderSide orderSide, string userId, string symbol, long quantity, decimal trailOffset)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var trailingStopOrder =
                await alpacaTradingClient.PostOrderAsync(orderSide.TrailingStop(symbol, quantity, TrailOffset.InPercent(trailOffset)));

            return trailingStopOrder.OrderId;
        }

        public static async Task<decimal> GetCurrentPrice(IConfiguration config, string userId, string symbol)
        {
            try
            {
                var alpacaDataClient = GetAlpacaDataClient(config, userId);

                var latestTrade = await alpacaDataClient.GetLatestTradeAsync(symbol);
                var snapshot = await alpacaDataClient.GetSnapshotAsync("CRCT");
                var previousClose = snapshot.PreviousDailyBar.Close;
                return latestTrade.Price;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static async Task<decimal> GetPreviousDayClose(IConfiguration config, string userId, string symbol)
        {
            try
            {
                var alpacaDataClient = GetAlpacaDataClient(config, userId);

                var snapshot = await alpacaDataClient.GetSnapshotAsync(symbol);
                return snapshot.PreviousDailyBar.Close;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static async Task<bool> CancelOrderByOrderId(IConfiguration config, string userId, Guid externalOrderId)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);

            try
            {
                var isOrderCanceled = await alpacaTradingClient.DeleteOrderAsync(externalOrderId);
                return isOrderCanceled;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling order in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<List<IPosition>> GetOpenPositions(IConfiguration config, string userId)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);

            try
            {
                var positions = await alpacaTradingClient.ListPositionsAsync();
                return positions.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling order in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<List<IOrder>> GetOpenOrders(IConfiguration config, string userId)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);

            try
            {
                var orders = await alpacaTradingClient.ListOrdersAsync(
                    new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open });
                return orders.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while getting open orders in Alpaca {e}: ", e);
                throw;
            }
        }

        public static async Task<List<IPositionActionStatus>> CloseOpenPositionsAndCancelExistingOrders(IConfiguration config, string userId)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);

            // Delete all open positions, cancels all open orders before liquidating
            try
            {
                var result = await alpacaTradingClient.DeleteAllPositionsAsync(new DeleteAllPositionsRequest { CancelOrders = true });
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

        public static async Task<ArchiveBlock> CloseOpenPositionAndCancelExistingOrders(IConfiguration config, string userId, string symbol)
        {
            var alpacaTradingClient = GetAlpacaTradingClient(config, userId);
            var accountType = await Queries.GetAccountTypeByUserId(userId);

            try
            {
                // Cancel open orders for symbol
                var orders = await alpacaTradingClient.ListOrdersAsync(new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open }.WithSymbol(symbol));
                foreach (var order in orders)
                {
                    var orderCancelResult = await alpacaTradingClient.DeleteOrderAsync(order.OrderId);
                }

                // Wait for orders to cancel before proceeding
                var waitingForOrdersToCancel = true;
                while (waitingForOrdersToCancel)
                {
                    Task.Delay(1000).Wait();
                    orders = await alpacaTradingClient.ListOrdersAsync(new ListOrdersRequest { OrderStatusFilter = OrderStatusFilter.Open }.WithSymbol(symbol));
                    var numOrders = orders.ToList().Count;
                    if (numOrders == 0)
                    {
                        waitingForOrdersToCancel = false;
                    }
                    Console.WriteLine($"Waiting for orders to cancel in Alpaca. Count is {numOrders}: ", numOrders);
                }

                // Delete open position for symbol ToDo: only delete position if position exists
                var positionData = await alpacaTradingClient.GetPositionAsync(symbol);
                var result = await alpacaTradingClient.DeletePositionAsync(new DeletePositionRequest(symbol));

                // Return a block to be archived (assume it sells since it is a market order - could create a new block for the order and wait for it to sell if it becomes an issue)
                var archiveBlock = new ArchiveBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = result.Symbol,
                    NumShares = result.IntegerQuantity,
                    ExternalBuyOrderId = new Guid(),
                    ExternalSellOrderId = result.OrderId,
                    ExternalStopLossOrderId = new Guid(),
                    BuyOrderFilledPrice = accountType == AccountTypes.SwingLong ? positionData.AverageEntryPrice : positionData.AssetCurrentPrice,
                    DateBuyOrderFilled = DateTime.Now,
                    DateSellOrderFilled = DateTime.Now,
                    SellOrderFilledPrice = accountType == AccountTypes.SwingLong ? positionData.AssetCurrentPrice : positionData.AverageEntryPrice,
                    Profit = positionData.UnrealizedProfitLoss // Actual profit / loss
                };

                return archiveBlock;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while canceling orders / closing positions in Alpaca {e}: ", e);
                return null;
            }
        }

        public static IAlpacaTradingClient GetAlpacaTradingClient(IConfiguration config, string userId)
        {
            var alpacaAPIKey = config.GetValue<string>("AlpacaPaperAPIKey" + ":" + userId);
            var alpacaAPISecret = config.GetValue<string>("AlpacaPaperAPISec" + ":" + userId);
            var alpacaTradingClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(alpacaAPIKey, alpacaAPISecret));
            return alpacaTradingClient;
        }
        public static IAlpacaDataClient GetAlpacaDataClient(IConfiguration config, string userId)
        {
            var alpacaAPIKey = config.GetValue<string>("AlpacaPaperAPIKey" + ":" + userId);
            var alpacaAPISecret = config.GetValue<string>("AlpacaPaperAPISec" + ":" + userId);
            var alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(alpacaAPIKey, alpacaAPISecret));
            return alpacaDataClient;
        }

    }
}
