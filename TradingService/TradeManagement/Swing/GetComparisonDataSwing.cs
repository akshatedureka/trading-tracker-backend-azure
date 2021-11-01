using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Order;
using TradingService.Common.Models;
using TradingService.Common.Repository;
using Alpaca.Markets;
using TradingService.TradeManagement.Swing.Transfer;

namespace TradingService.TradeManagement.Swing
{
    public class GetComparisonDataSwing
    {
        private readonly IConfiguration _configuration;

        public GetComparisonDataSwing(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("GetComparisonDataSwing")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get comparison data.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            // The name of the database and container we will create
            const string databaseId = "Tracker";
            const string containerIdForBlocks = "Blocks";

            var containerForBlocks = await Repository.GetContainer(databaseId, containerIdForBlocks);

            // ToDo: Create common DB query in Common project to use throughout functions
            // Get open user blocks
            var userBlocks = new List<UserBlock>();
            try
            {
                userBlocks = containerForBlocks
                    .GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting blocks from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting blocks {ex}", ex);
                return new BadRequestObjectResult("Error getting blocks:" + ex);
            }

            // Get open orders
            var openOrders = await Order.GetOpenOrders(_configuration, userId);

            // Create a list of comparison data to return
            var comparisonData = new List<ComparisonDataTransfer>();

            // Run comparison logic and populate comparison data
            foreach (var userBlock in userBlocks)
            {
                // Get blocks with open buy or sell orders
                var symbol = userBlock.Symbol;
                var openBuyOrderBlocks = userBlock.Blocks.Where(b => b.BuyOrderCreated && !b.SellOrderCreated);
                var openSellOrderBlocks = userBlock.Blocks.Where(b => b.SellOrderCreated);
                var openBuyOrdersForSymbol = openOrders.Where(o => o.Symbol == symbol && o.OrderSide == OrderSide.Buy);
                var openSellOrdersForSymbol = openOrders.Where(o => o.Symbol == symbol && o.OrderSide == OrderSide.Sell);

                // Cycle through open buy order blocks and see if buy order exists in external system
                foreach (var openBuyOrderBlock in openBuyOrderBlocks)
                {
                    var comparisonBlock = CreateComparisonDataFromBlock(openBuyOrderBlock);
                    var externalBuyOrderId = openBuyOrderBlock.ExternalBuyOrderId;
                    foreach (var buyOrder in openBuyOrdersForSymbol)
                    {
                        if (buyOrder.OrderId != externalBuyOrderId)
                        {
                            continue;
                        }
                        comparisonBlock.hasDiscrepancy = false;
                        break;
                    }

                    comparisonData.Add(comparisonBlock);
                }

                // Cycle through open sell order blocks and see if a sell order exists in external system
                foreach (var openSellOrderBlock in openSellOrderBlocks)
                {
                    var comparisonBlock = CreateComparisonDataFromBlock(openSellOrderBlock);
                    var externalSellOrderId = openSellOrderBlock.ExternalSellOrderId;
                    foreach (var sellOrder in openSellOrdersForSymbol)
                    {
                        if (sellOrder.OrderId != externalSellOrderId)
                        {
                            continue;
                        }
                        comparisonBlock.hasDiscrepancy = false;
                        break;
                    }

                    comparisonData.Add(comparisonBlock);
                }
            }

            return new OkObjectResult(comparisonData);
        }

        private ComparisonDataTransfer CreateComparisonDataFromBlock(Block block)
        {
            return new ComparisonDataTransfer
            {
                BlockId = block.Id,
                BuyOrderCreated = block.BuyOrderCreated,
                BuyOrderFilled = block.BuyOrderFilled,
                BuyOrderFilledPrice = block.BuyOrderFilledPrice,
                DateBuyOrderFilled = block.DateBuyOrderFilled,
                SellOrderCreated = block.SellOrderCreated,
                ExternalBuyOrderId = block.ExternalBuyOrderId,
                ExternalSellOrderId = block.ExternalSellOrderId,
                ExternalStopLossOrderId = block.ExternalStopLossOrderId,
                hasDiscrepancy = true
            };
        }
    }
}
