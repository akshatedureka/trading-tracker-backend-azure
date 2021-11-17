using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Order;
using TradingService.Common.Repository;

namespace TradingService.TradeManagement.Swing
{
    public class StopTrading
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public StopTrading(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        [FunctionName("StopTrading")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            log.LogInformation($"Function executed to stop swing trading for user {userId} and symbol {symbol}.");
                                
            try
            {
                // Turn trading off
                var updateTradingStatusReponse = await _queries.UpdateTradingStatusForSymbol(userId, symbol);

                // Cancel order and close positions, return closed block information
                var closedBlock = await _order.CloseOpenPositionAndCancelExistingOrders(_configuration, userId, symbol);
                if (closedBlock is null) // ToDo: split closing orders and positions. There may not be any open positions. Handle this error so that other real errors get caught and returned to the user.
                {
                    log.LogInformation("No open positions.");
                    return new OkObjectResult("There are no open positions to close.");
                }

                // Get closed blocks
                var closedBlocks = await _queries.GetClosedBlocksByUserIdAndSymbol(userId, symbol);

                // Move closed blocks to one condensed block
                var profit = closedBlock.Profit;
                foreach (var block in closedBlocks)
                {
                    profit += block.Profit;
                }

                await _queries.CreateCondensedBlockByUserIdAndSymbol(userId, symbol, profit);

                // Delete closed blocks
                await _queries.DeleteClosedBlocksByClosedBlocks(closedBlocks);

                // Reset blocks
                await _queries.ResetUserBlockByUserIdAndSymbol(userId, symbol);

                log.LogInformation($"Stopped trading for user {userId} and symbol {symbol} at {DateTimeOffset.Now}.");

                return new OkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
        }
    }
}
