using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Order;
using TradingService.Common.Repository;

namespace TradingService.OrderManagement
{
    public class CloseOpenPosition
    {
        private readonly IConfiguration _configuration;

        public CloseOpenPosition(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CloseOpenPosition")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to close open positions for symbol.");

            const string containerId = "BlocksClosed";
            var container = await Repository.GetContainer(containerId);

            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            try
            {
                var closedBlock = await Order.CloseOpenPositionAndCancelExistingOrders(_configuration, userId, symbol);
                if (closedBlock is null) // ToDo: split closing orders and positions. There may not be any open positions. Handle this error so that other real errors get caught and returned to the user.
                {
                    Console.WriteLine("Error closing open positions");
                    return new OkObjectResult("There are no open positions to close.");
                }

                // ToDo: Move closed block to common module
                await container.CreateItemAsync(closedBlock, new PartitionKey(closedBlock.UserId));

                log.LogInformation($"Created closed block record for block id {closedBlock.Id} at: {DateTimeOffset.Now}.");

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
