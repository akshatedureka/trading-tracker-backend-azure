using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Order;

namespace TradingService.OrderManagement
{
    public class CloseAllOpenPositions
    {
        private readonly IConfiguration _configuration;
        private static Container _containerArchive;
        private static readonly string databaseId = "Tracker";
        private static readonly string containerArchiveId = "BlocksArchive";

        public CloseAllOpenPositions(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("CloseAllOpenPositions")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to close all open positions.");
            var userId = req.Headers["From"].FirstOrDefault();

            dynamic result = await Order.CloseOpenPositionsAndCancelExistingOrders(_configuration, userId);

            //ToDo: Create entry in block archive table for each symbol

            return new OkObjectResult(JsonConvert.SerializeObject(result));
        }
    }
}
