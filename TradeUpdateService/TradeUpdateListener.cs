using System;
using System.Threading;
using System.Threading.Tasks;
using Alpaca.Markets;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TradeUpdateService.Enums;
using TradeUpdateService.Models;
using Console = System.Console;

namespace TradeUpdateService
{
    public class TradeUpdateListener : ITradeUpdateListener
    {
        private readonly IConfiguration _config;
        private IAlpacaStreamingClient _alpacaStreamingClient;
        private QueueClient _queueClient;
        private string _userId;
        private bool _connectionError = false;

        public TradeUpdateListener(IConfiguration config)
        {
            _config = config;
        }

        public async Task StartListening(string userId, AccountTypes accountType, string alpacaKey, string alpacaSecret)
        {
            Console.WriteLine("I'm listening as user " + userId);

             _userId = userId;

            var queueName = accountType == AccountTypes.Swing ? "tradeupdatequeueswing" : "tradeupdatequeuedaymarket";

            // Get the connection string from app settings
            var connectionString = _config.GetValue<string>("AzureWebJobsStorage");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            _queueClient = new QueueClient(connectionString, queueName);

            // Create the queue if it doesn't already exist
            _queueClient.CreateIfNotExists();

            // Connect to Alpaca's websocket and listen for updates on our orders
            _alpacaStreamingClient =
                Environments.Paper.GetAlpacaStreamingClient(new SecretKey(alpacaKey, alpacaSecret));

            await TryToConnectToAlpaca();

            _alpacaStreamingClient.OnTradeUpdate += HandleTradeUpdate;
            _alpacaStreamingClient.OnError += HandleTradeError;

            while (true)
            {
                if (!_connectionError) continue;

                // Attempt Reconnect
                Console.WriteLine("Error with Alpaca tcp connection. Reconnecting... {0}", DateTimeOffset.Now);
                await TryToConnectToAlpaca();

                _connectionError = false;
                Console.WriteLine("Reconnected to Alpaca...{0}", DateTimeOffset.Now);
            }
        }

        private void HandleTradeUpdate(ITradeUpdate trade)
        {
            var orderId = trade.Order.OrderId;
            var orderSide = trade.Order.OrderSide;
            var symbol = trade.Order.Symbol;
            var numShares = trade.Order.FilledQuantity;

            Console.WriteLine("Handling trade update as user " + _userId);

            switch (trade.Event)
            {
                case TradeEvent.New:
                    // Do nothing if new buy / sell order created
                    break;
                case TradeEvent.Fill:
                    if (orderSide == OrderSide.Buy) // ToDo: remove if statement as the logic is the same for both buy and sell
                    {
                        try
                        {
                            var executedPrice = (decimal)trade.Price;
                            var msg = new OrderUpdateMessage
                            { UserId = _userId, Symbol = symbol, OrderId = orderId, OrderSide = orderSide, ExecutedPrice = executedPrice };

                            // Send a message to the queue
                            _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));
                            Console.WriteLine("Published buy order fill for order {0} for symbol {1} quantity {2} at price {3} at: {4}", orderId, symbol, numShares, executedPrice, DateTimeOffset.Now);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception thrown while publishing buy order executed at: {0} : {1}", DateTimeOffset.Now, ex);
                        }
                    }
                    else
                    {
                        try
                        {
                            var executedPrice = (decimal)trade.Price;
                            var msg = new OrderUpdateMessage
                            { UserId = _userId, Symbol = symbol, OrderId = orderId, OrderSide = orderSide, ExecutedPrice = executedPrice };

                            // Send a message to the queue
                            _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));
                            Console.WriteLine("Published sell order fill for order {0} for symbol {1} quantity {2} at price {3} at: {4}", orderId, symbol, numShares, executedPrice, DateTimeOffset.Now);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception thrown while publishing sell order executed at: {0} : {1}", DateTimeOffset.Now, ex);
                        }
                    }
                    break;
                case TradeEvent.Rejected:
                    // ToDo
                    var price = (decimal)trade.Price;
                    Console.WriteLine("Rejected order {0} for price {1} at: {2}", orderId, price, DateTimeOffset.Now);
                    break;
                case TradeEvent.Canceled:
                    var numSharesCanceled = trade.Order.Quantity;
                    Console.WriteLine("Sell order {0} was cancelled for symbol {1} quantity {2} at: {3}", orderId, symbol, numSharesCanceled, DateTimeOffset.Now);
                    break;
                case TradeEvent.PartialFill:
                    // ToDo
                    break;
                case TradeEvent.DoneForDay:
                    // ToDo
                    break;
            }
        }

        private async Task TryToConnectToAlpaca()
        {
            var connectionStatus = await _alpacaStreamingClient.ConnectAndAuthenticateAsync();
            var curThread = Thread.CurrentThread.ManagedThreadId;

            if (connectionStatus == AuthStatus.Authorized)
            {
                Console.WriteLine("Connected to Alpaca Streaming Client on thread id {0}", curThread);
            }
            else
            {
                var retryAttempt = 0;
                var connectionStatusRetry = AuthStatus.Unauthorized;
                while (connectionStatusRetry == AuthStatus.Unauthorized)
                {
                    connectionStatusRetry = await _alpacaStreamingClient.ConnectAndAuthenticateAsync();
                    await Task.Delay(1000); // wait one second in between attempts
                    Console.WriteLine("Failed to connect to Alpaca Streaming Client. Retry attempt {0}", retryAttempt + 1);
                    retryAttempt++;
                }

                Console.WriteLine("Connected to Alpaca Streaming Client on thread id {0}", curThread);
            }
        }

        private void HandleTradeError(Exception ex)
        {
            Console.WriteLine("Trade error occurred with Alpaca at: {0} : {1}", DateTimeOffset.Now, ex);
            _connectionError = true;
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
