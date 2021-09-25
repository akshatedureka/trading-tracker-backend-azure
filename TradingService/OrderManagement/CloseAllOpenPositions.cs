using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Order;

namespace TradingService.OrderManagement
{
    public static class CloseAllOpenPositions
    {
        [FunctionName("CloseAllOpenPositions")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to close all open positions.");

            dynamic result = await Order.CloseOpenPositionsAndCancelExistingOrders();

            //ToDo: Create entry in block archive table for each symbol

            return new OkObjectResult(JsonConvert.SerializeObject(result));
        }
    }
}
