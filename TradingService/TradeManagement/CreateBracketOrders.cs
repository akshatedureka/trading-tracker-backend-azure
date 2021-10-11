using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace TradingService.TradeManagement
{
    public class CreateBracketOrders
    {
        private readonly IConfiguration _configuration;

        public CreateBracketOrders(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CreateBracketOrders")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var userId = req.Headers["From"].FirstOrDefault();
            string name = req.Query["symbol"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // Loop through symbol list
            var symbols = new List<string>
            {
                "CRCT",
                "SPCE",
                "T",
                "HYLN"
            };

            foreach (var symbol in symbols)
            {
                // Get previous day close
                var currentPrice = await Order.GetCurrentPrice(_configuration, userId, symbol);
                var previousDayClose = await Order.GetPreviousDayClose(_configuration, userId, symbol);

                if (currentPrice < previousDayClose) // ToDo: And no open day block for symbol
                {
                    var stopPrice = previousDayClose;
                    var limitPrice = stopPrice + (decimal)0.05;
                    var takeProfitLimitPrice = limitPrice + (decimal)0.10;
                    var stopLossPrice = previousDayClose - (decimal)0.05;
                    var quantity = 100;

                    // every one minute, cancel and do a new order if not filled to reset price
                    var orderIds = await Order.CreateStopLimitBracketOrder(_configuration, OrderSide.Buy, userId, name, quantity, stopPrice, limitPrice, takeProfitLimitPrice, stopLossPrice);
                    var buyOrderId = orderIds.BuyOrderId;
                    var sellOrderId = orderIds.SellOrderId;
                    var stopLossOrderId = orderIds.StopLossOrderId;

                    // ToDo: Update to a per user archive table
                    // Record block in new day trade archive table
                    var userBlock = new UserBlock
                    {
                        Id = Guid.NewGuid().ToString(),
                        Symbol = symbol,
                        NumShares = quantity
                    };

                    var dayBlock = new Block
                    {
                        Id = Guid.NewGuid().ToString(),
                        ExternalBuyOrderId = buyOrderId,
                        ExternalSellOrderId = sellOrderId,
                        ExternalStopLossOrderId = stopLossOrderId,
                        BuyOrderPrice = limitPrice,
                        SellOrderPrice = takeProfitLimitPrice,
                        StopLossOrderPrice = stopLossPrice,
                        DayBlock = true
                    };

                    userBlock.Blocks.Add(dayBlock);


                    // The Azure Cosmos DB endpoint for running this sample.
                    var endpointUri = Environment.GetEnvironmentVariable("EndPointUri");

                    // The primary key for the Azure Cosmos account.
                    var primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

                    // The name of the database and container we will create
                    var databaseId = "Tracker";
                    var containerId = "ArchiveBlocksDay";

                    // Connect to Cosmos DB using endpoint
                    var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
                    var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                    var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/UserId");

                    // Save block to Cosmos DB
                    try
                    {
                        var blockResponse = await container.CreateItemAsync(userBlock, new PartitionKey(userBlock.UserId));
                    }
                    catch (CosmosException ex)
                    {
                        log.LogError("Issue creating Cosmos DB item {ex}", ex);
                    }

                    log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", name, limitPrice);

                }
            }

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
