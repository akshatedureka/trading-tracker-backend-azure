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
using TradingService.TradeManagement.Transfer;

namespace TradingService.TradeManagement
{
    public class GetTradingData
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public GetTradingData(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        [FunctionName("GetTradingData")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get symbols.");
            var userId = req.Headers["From"].FirstOrDefault();

            // The name of the database and container we will create
            const string containerIdForClosedBlocks = "BlocksClosed";
            const string containerIdForCondensedlocks = "BlocksCondensed";

            var containerForBlocksClosed = await _repository.GetContainer(containerIdForClosedBlocks);
            var containerForBlocksCondensed = await _repository.GetContainer(containerIdForCondensedlocks);

            // Get symbol data
            var userSymbol = await _queries.GetUserSymbolByUserId(userId);

            if (userSymbol == null)
            {
                return new NoContentResult();
            }

            // Get ladder data
            var userLadder = await _queries.GetLaddersByUserId(userId);

            if (userLadder == null)
            {
                return new NoContentResult();
            }

            var ladderswithBlocks = userLadder.Ladders.Where(l => l.BlocksCreated);
            var symbolsWithBlocksCreated = (userSymbol.Symbols.SelectMany(symbol => ladderswithBlocks.Where(ladder => ladder.Symbol == symbol.Name).Select(ladder => symbol))).ToList();

            // Add symbol data to return object
            var tradingData = symbolsWithBlocksCreated.Select(symbol => new TradingData { SymbolId = symbol.Id, Symbol = symbol.Name, Active = symbol.Active, Trading = symbol.Trading }).ToList();

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

            if (condensedUserBlock != null)
            {
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
            }

            // Add in position data
            var positions = await _order.GetOpenPositions(_configuration, userId);

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
