using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.SymbolManagement.Models;
using TradingService.TradeManagement.Swing.Transfer;

namespace TradingService.TradeManagement.Swing
{
    public class GetTradingDataSwing
    {
        private readonly IConfiguration _configuration;

        public GetTradingDataSwing(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("GetTradingDataSwing")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");
            var userId = req.Headers["From"].FirstOrDefault();

            // The name of the database and container we will create
            const string containerIdForSymbols = "Symbols";
            const string containerIdForClosedBlocks = "BlocksClosed";
            const string containerIdForCondensedlocks = "BlocksCondensed";

            var containerForSymbols = await Repository.GetContainer(containerIdForSymbols);
            var containerForBlocksClosed = await Repository.GetContainer(containerIdForClosedBlocks);
            var containerForBlocksCondensed = await Repository.GetContainer(containerIdForCondensedlocks);

            // Get symbol data
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
                log.LogError($"Issue getting symbols from Cosmos DB item {ex.Message}.");
                return new BadRequestObjectResult($"Error getting symbols from DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting symbols {ex.Message}.");
                return new BadRequestObjectResult($"Error getting symbols: {ex.Message}.");
            }

            // Add symbol data to return object
            var tradingData = symbols.Select(symbol => new TradingData { SymbolId = symbol.Id, Symbol = symbol.Name, Active = symbol.Active, Trading = symbol.Trading }).ToList();

            // Get closed block data
            var closedBlocks = new List<ClosedBlock>();

            // Read closed blocks from Cosmos DB
            try
            {
                closedBlocks = containerForBlocksClosed
                    .GetItemLinqQueryable<ClosedBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId).ToList();
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting closed blocks from Cosmos DB item {ex.Message}.");
                return new BadRequestObjectResult($"Error getting closed blocks from DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting closed blocks {ex.Message}.");
                return new BadRequestObjectResult($"Error getting closed blocks: {ex.Message}.");
            }

            // Get condensed block data
            var condensedUserBlock = new UserCondensedBlock();

            // Read condensed blocks from Cosmos DB
            try
            {
                condensedUserBlock = containerForBlocksCondensed
                    .GetItemLinqQueryable<UserCondensedBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId).ToList().FirstOrDefault();
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting condensed blocks from Cosmos DB item {ex.Message}.");
                return new BadRequestObjectResult($"Error getting condensed blocks from DB: {ex.Message}.");
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting condensed blocks {ex.Message}.");
                return new BadRequestObjectResult($"Error getting condensed blocks: {ex.Message}.");
            }

            // Calculate profit for closed blocks
            foreach (var closedBlock in closedBlocks)
            {
                foreach (var tradeData in tradingData.Where(tradeData => closedBlock.Symbol == tradeData.Symbol))
                {
                    tradeData.ClosedProfit += closedBlock.Profit;
                }
            }

            // Calculate profit for condensed blocks
            foreach (var tradeData in tradingData)
            {
                foreach (var condensedBlock in condensedUserBlock.CondensedBlocks)
                {
                    if (tradeData.Symbol == condensedBlock.Symbol)
                    {
                        tradeData.CondensedProfit = condensedBlock.Profit;
                    }
                }
            }

            // Add in position data
            var positions = await Order.GetOpenPositions(_configuration, userId);

            foreach (var position in positions)
            {
                foreach (var tradeData in tradingData.Where(t => position.Symbol == t.Symbol))
                {
                    tradeData.CurrentQuantity = position.Quantity;
                    tradeData.OpenProfit = position.UnrealizedProfitLoss;
                }
            }

            // Calculate total profit
            foreach (var tradeData in tradingData)
            {
                tradeData.TotalProfit = tradeData.OpenProfit + tradeData.ClosedProfit + tradeData.CondensedProfit;
            }

            return new OkObjectResult(JsonConvert.SerializeObject(tradingData));
        }
    }
}
