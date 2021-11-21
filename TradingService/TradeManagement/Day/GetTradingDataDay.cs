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
using TradingService.SymbolManagement.Models;
using TradingService.TradeManagement.Day.Models;

namespace TradingService.TradeManagement.Day
{
    public class GetTradingDataDay
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public GetTradingDataDay(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        [FunctionName("GetTradingDataDay")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");

            var userId = req.Headers["From"].FirstOrDefault();

            // The name of the database and container we will create
            const string containerIdForSymbols = "Symbols";
            const string containerIdForBlockDayArchive = "BlocksDayArchive";

            var containerForSymbols = await _repository.GetContainer(containerIdForSymbols);
            var containerForDayBlockArchive = await _repository.GetContainer(containerIdForBlockDayArchive);

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

            var tradingData = symbols.Select(symbol => new TradingData { SymbolId = symbol.Id, Symbol = symbol.Name, Active = symbol.Active, Trading = symbol.Trading }).ToList();

            // Add in archive data
            var archiveBlocks = new List<ClosedBlock>();

            // Read archive blocks from Cosmos DB
            try
            {
                archiveBlocks = containerForDayBlockArchive
                    .GetItemLinqQueryable<ClosedBlock>(allowSynchronousQueryExecution: true)
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
            var positions = await _order.GetOpenPositions(_configuration, userId);

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
