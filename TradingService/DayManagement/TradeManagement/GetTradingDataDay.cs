using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Order;
using TradingService.Common.Models;
using TradingService.Common.Repository;
using TradingService.DayManagement.SymbolManagement.Models;
using TradingService.DayManagement.TradeManagement.Models;

namespace TradingService.DayManagement.TradeManagement.Day
{
    public class GetTradingDataDay
    {
        private readonly IConfiguration _configuration;

        public GetTradingDataDay(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("GetTradingDataDay")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");

            var userId = req.Headers["From"].FirstOrDefault();

            // The name of the database and container we will create
            const string databaseId = "Tracker";
            const string containerIdForSymbols = "Symbols";
            const string containerIdForBlockDayArchive = "BlocksDayArchive";

            var containerForSymbols = await Repository.GetContainer(databaseId, containerIdForSymbols);
            var containerForDayBlockArchive = await Repository.GetContainer(databaseId, containerIdForBlockDayArchive);

            var symbols = new List<Symbol>();

            // Read symbols from Cosmos DB
            try
            {
                var userSymbolResponse = containerForSymbols
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();
                if (userSymbolResponse != null)
                {
                    symbols = userSymbolResponse.Symbols;
                }
                else
                {
                    return new NoContentResult();
                }
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting symbols from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting symbols from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting symbols {ex}", ex);
                return new BadRequestObjectResult("Error getting symbols:" + ex);
            }

            var tradingData = symbols.Select(symbol => new TradingData { SymbolId = symbol.Id, Symbol = symbol.Name, Active = symbol.Active, Trading = symbol.DayTrading }).ToList();

            // Add in archive data
            var archiveBlocks = new List<ArchiveBlock>();

            // Read archive blocks from Cosmos DB
            try
            {
                archiveBlocks = containerForDayBlockArchive
                    .GetItemLinqQueryable<ArchiveBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue getting archive blocks from Cosmos DB item {ex}", ex);
                return new BadRequestObjectResult("Error getting archive blocks from DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue getting archive blocks {ex}", ex);
                return new BadRequestObjectResult("Error getting archive blocks:" + ex);
            }

            // Calculate profit for blocks
            foreach (var archiveBlock in archiveBlocks)
            {
                foreach (var tradeData in tradingData.Where(tradeData => archiveBlock.Symbol == tradeData.Symbol))
                {
                    tradeData.ArchiveProfit += archiveBlock.Profit;
                }
            }

            // Add in position data
            var positions = await Order.GetOpenPositions(_configuration, userId);

            foreach (var position in positions)
            {
                foreach (var tradeData in tradingData.Where(t => position.Symbol == t.Symbol))
                {
                    tradeData.CurrentQuantity = position.Quantity;
                    tradeData.CurrentProfit = position.UnrealizedProfitLoss;
                }
            }

            // Calculate total profit
            foreach (var tradeData in tradingData)
            {
                tradeData.TotalProfit = tradeData.CurrentProfit + tradeData.ArchiveProfit;
            }

            return new OkObjectResult(JsonConvert.SerializeObject(tradingData));
        }
    }
}