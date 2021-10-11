using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;

namespace TradingService.OrderManagement
{
    public class CloseOpenPosition
    {
        private readonly IConfiguration _configuration;
        private static Container _containerArchive;
        private static readonly string databaseId = "Tracker";
        private static readonly string containerArchiveId = "BlocksArchive";

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
            
            _containerArchive = await Repository.GetContainer(databaseId, containerArchiveId);

            // Get symbol name
            string symbol = req.Query["symbol"];
            var userId = req.Headers["From"].FirstOrDefault();

            try
            {
                var archiveBlock = await Order.CloseOpenPositionAndCancelExistingOrders(_configuration, userId, symbol);
                if (archiveBlock is null)
                {
                    Console.WriteLine("Error closing open positions");
                    return new BadRequestObjectResult("There are no open positions.");
                }

                // ToDo: Move archive block to common module
                await _containerArchive.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));

                log.LogInformation("Created archive record for block id {archiveBlock.Id} at: {time}", archiveBlock.Id, DateTimeOffset.Now);

                return new OkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new BadRequestObjectResult(ex.Message);
            }
            
        }
    }
}
