using System;
using System.IO;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Order;

namespace TradingService.TradeManagement
{
    public static class CreateTakeProfitOrder
    {
        [FunctionName("CreateTakeProfitOrder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["symbol"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            var currentPrice = await Order.GetCurrentPrice(name);
            var stopPrice = currentPrice + (decimal) 0.05;
            var limitPrice = stopPrice + (decimal) 0.01; 
            var takeProfitLimitPrice = limitPrice + (decimal) 0.01;
            var stopLossPrice = limitPrice - (decimal) 0.05;

            // every one minute, cancel and do a new order if not filled to reset price
            var orderId = await Order.CreateBracketOrder(OrderSide.Buy, name, 100, stopPrice, limitPrice, takeProfitLimitPrice, stopLossPrice);
            log.LogInformation("Created bracket order for symbol {symbol} for limit price {limitPrice}", name, limitPrice);

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
