using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Order;
using TradingService.Common.Models;
using TradingService.Common.Repository;
using Alpaca.Markets;
using TradingService.TradeManagement.Swing.Transfer;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Enums;

namespace TradingService.TradeManagement.Swing
{
    public class GetComparisonDataSwing
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly ITradeOrder _order;
        private readonly IAccountItemRepository _accountRepo;

        public GetComparisonDataSwing(IConfiguration configuration, IQueries queries, ITradeOrder order, IAccountItemRepository accountRepo)
        {
            _configuration = configuration;
            _queries = queries;
            _order = order;
            _accountRepo = accountRepo;
        }

        [FunctionName("GetComparisonDataSwing")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get comparison data.");
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            var accountType = await _accountRepo.GetAccountTypeByUserId(userId);
            var symbols = await _queries.GetActiveTradingSymbolsByUserId(userId);
            var blocks = await _queries.GetBlocksByUserIdAndSymbols(userId, symbols);

            // Get open orders
            var openOrders = await _order.GetOpenOrders(_configuration, userId);

            // Create a list of comparison data to return
            var comparisonData = new List<ComparisonDataTransfer>();

            // Run comparison logic and populate comparison data
            foreach (var symbol in symbols)
            {
                // Get blocks with open buy or sell orders
                var openBuyOrderBlocks = new List<Block>();
                var openSellOrderBlocks = new List<Block>();

                if (accountType == AccountTypes.SwingLong)
                {
                    openBuyOrderBlocks = blocks.Where(b => (b.BuyOrderCreated && !b.SellOrderCreated) && b.Symbol == symbol.Name).ToList();
                    openSellOrderBlocks = blocks.Where(b => b.SellOrderCreated && b.Symbol == symbol.Name).ToList();
                }
                else
                {
                    openBuyOrderBlocks = blocks.Where(b => b.BuyOrderCreated && b.Symbol == symbol.Name).ToList();
                    openSellOrderBlocks = blocks.Where(b => (b.SellOrderCreated && !b.BuyOrderCreated) && b.Symbol == symbol.Name).ToList();
                }

                var openBuyOrdersForSymbol = openOrders.Where(o => o.Symbol == symbol.Name && o.OrderSide == OrderSide.Buy);
                var openSellOrdersForSymbol = openOrders.Where(o => o.Symbol == symbol.Name && o.OrderSide == OrderSide.Sell);

                // Cycle through open buy order blocks and see if buy order exists in external system
                foreach (var openBuyOrderBlock in openBuyOrderBlocks)
                {
                    var comparisonBlock = CreateComparisonDataFromBlock(symbol.Name, openBuyOrderBlock);
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
                    var comparisonBlock = CreateComparisonDataFromBlock(symbol.Name, openSellOrderBlock);
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

        private ComparisonDataTransfer CreateComparisonDataFromBlock(string symbol, Block block)
        {
            return new ComparisonDataTransfer
            {
                Symbol = symbol,
                BlockId = block.Id,
                BuyOrderCreated = block.BuyOrderCreated,
                BuyOrderFilled = block.BuyOrderFilled,
                BuyOrderPrice = block.BuyOrderPrice,
                BuyOrderFilledPrice = block.BuyOrderFilledPrice,
                DateBuyOrderFilled = block.DateBuyOrderFilled,
                SellOrderCreated = block.SellOrderCreated,
                SellOrderFilled = block.SellOrderFilled,
                SellOrderPrice = block.SellOrderPrice,
                SellOrderFilledPrice = block.SellOrderFilledPrice,
                DateSellOrderFilled = block.DateSellOrderFilled,
                ExternalBuyOrderId = block.ExternalBuyOrderId,
                ExternalSellOrderId = block.ExternalSellOrderId,
                ExternalStopLossOrderId = block.ExternalStopLossOrderId,
                hasDiscrepancy = true
            };
        }
    }
}
