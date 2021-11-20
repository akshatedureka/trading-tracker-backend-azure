using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TradingService.TradeManagement.Swing
{
    public static class GetOpenPositions
    {
        [FunctionName("GetOpenPositions")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get open positions.");

            // ToDo: Verify this is really not used anymore
            //var positions = await Order.GetOpenPositions();
            //var positionsToReturn = positions.Select(position => new Position { Symbol = position.Symbol, Quantity = position.Quantity, Profit = position.UnrealizedProfitLoss }).ToList();
            //return new OkObjectResult(JsonConvert.SerializeObject(positionsToReturn));
            return new OkResult();
        }
    }
}
