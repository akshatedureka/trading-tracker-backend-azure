using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Alpaca.Markets;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Environments = Alpaca.Markets.Environments;
using Newtonsoft.Json;

namespace TradeUpdateService
{
    public class OrderUpdateProducer : BackgroundService
    {
        private readonly ILogger<OrderUpdateProducer> _logger;
        private readonly IConfiguration _config;
        private IAlpacaStreamingClient _alpacaStreamingClient;
        private QueueClient _queueClient;
        private bool _connectionError = false;

        public OrderUpdateProducer(ILogger<OrderUpdateProducer> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // Get the connection string from app settings
            var connectionString = _config.GetValue<string>("AzureWebJobsStorage");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            _queueClient = new QueueClient(connectionString, "tradeupdatequeue");

            // Create the queue if it doesn't already exist
            _queueClient.CreateIfNotExists();

            // Connect to Alpaca's websocket and listen for updates on our orders
            var apiKey = _config.GetValue<string>("AlpacaPaperAPIKey");
            var apiSecret = _config.GetValue<string>("AlpacaPaperAPISecret");

            _alpacaStreamingClient =
                Environments.Paper.GetAlpacaStreamingClient(new SecretKey(apiKey, apiSecret));

            _logger.LogInformation("Connecting to Alpaca Streaming Client for producer.");

            await TryToConnectToAlpaca(cancellationToken);

            _alpacaStreamingClient.OnTradeUpdate += HandleTradeUpdate;
            _alpacaStreamingClient.OnError += HandleTradeError;

            await ExecuteAsync(cancellationToken);


            //FOR TESTING ONLY!
            //for (var x = 0; x < 100; x++)
            //{
            //    await Task.Delay(1000);
            //    var msg = new OrderUpdateMessage
            //    {
            //        OrderId = new Guid("00000000-0000-0000-0000-000000000001"), OrderSide = OrderSide.Buy,
            //        ExecutedPrice = 27.46M
            //    };
            //    _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));

            //    await Task.Delay(1000);

            //    msg = new OrderUpdateMessage
            //    {
            //        OrderId = new Guid("00000000-0000-0000-0000-000000000002"), OrderSide = OrderSide.Sell,
            //        ExecutedPrice = 27.56M
            //    };
            //    _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));
            //}
        }

        // ToDo: Call this method when it is time to stop running
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested) continue;
                    if (!_connectionError) continue;

                    // Attempt Reconnect
                    _logger.LogInformation("Error with Alpaca tcp connection. Reconnecting...", DateTimeOffset.Now);
                    await Task.Delay(1000, cancellationToken); // wait one second
                    await TryToConnectToAlpaca(cancellationToken);
                    
                    _connectionError = false;
                    _logger.LogInformation("Reconnected to Alpaca...", DateTimeOffset.Now);
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Sender is stopping");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Sender had error");
            }

        }

        public override void Dispose()
        {
            _alpacaStreamingClient?.Dispose();
        }

        private void HandleTradeUpdate(ITradeUpdate trade)
        {
            var orderId = trade.Order.OrderId;
            var orderSide = trade.Order.OrderSide;
            
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
                                    { OrderId = orderId, OrderSide = orderSide, ExecutedPrice = executedPrice };

                            // Send a message to the queue
                            _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));
                            _logger.LogInformation("Published buy order fill for order {orderId} for price {executedPrice} at: {time}", orderId, executedPrice, DateTimeOffset.Now);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Exception thrown while publishing buy order executed at: {time} : {ex}", DateTimeOffset.Now, ex);
                        }
                    }
                    else
                    {
                        try
                        {
                            var executedPrice = (decimal)trade.Price;
                            var msg = new OrderUpdateMessage
                                { OrderId = orderId, OrderSide = orderSide, ExecutedPrice = executedPrice };

                            // Send a message to the queue
                            _queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));
                            _logger.LogInformation("Published sell order fill for order {orderId} for price {executedPrice} at: {time}", orderId, executedPrice, DateTimeOffset.Now);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation("Exception thrown while publishing sell order executed at: {time} : {ex}", DateTimeOffset.Now, ex);
                        }
                    }
                    break;
                case TradeEvent.Rejected:
                    // ToDo
                    var price = (decimal)trade.Price;
                    _logger.LogInformation("Rejected order {orderId} for price {price} at: {time}", orderId, price, DateTimeOffset.Now);
                    break;
                case TradeEvent.Canceled:
                    //await unitOfWork.Blocks.UpdateBuyOrderCanceled(orderId);
                    //await unitOfWork.CompleteAsync();
                    break;
                case TradeEvent.PartialFill:
                    // ToDo
                    break;
                case TradeEvent.DoneForDay:
                    // ToDo
                    break;
            }
        }
        
        private void HandleTradeError(Exception ex)
        {
            _logger.LogInformation("Trade error occurred with Alpaca at: {time} : {ex}", DateTimeOffset.Now, ex);
            _connectionError = true;
        }

        private async Task TryToConnectToAlpaca(CancellationToken cancellationToken)
        {
            var connectionStatus = await _alpacaStreamingClient.ConnectAndAuthenticateAsync(cancellationToken);
            var curThread = Thread.CurrentThread.ManagedThreadId;

            if (connectionStatus == AuthStatus.Authorized)
            {
                _logger.LogInformation("Connected to Alpaca Streaming Client on thread id {curThread}", curThread);
            }
            else
            {
                var retryAttempt = 0;
                var connectionStatusRetry = AuthStatus.Unauthorized;
                while (connectionStatusRetry == AuthStatus.Unauthorized)
                {
                    connectionStatusRetry = await _alpacaStreamingClient.ConnectAndAuthenticateAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken); // wait one second in between attempts
                    _logger.LogInformation("Failed to connect to Alpaca Streaming Client. Retry attempt {attempt}", retryAttempt);
                    retryAttempt++;
                }

                _logger.LogInformation("Connected to Alpaca Streaming Client on thread id {curThread}", curThread);
            }
        }
        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}