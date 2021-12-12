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
using TradingService.Core.Models;
using TradingService.Infrastructure.Services.Interfaces;
using TradingService.Core.Interfaces.Persistence;

namespace TradingService.Functions.TradeManagement
{
    public class GetTradingAccountData
    {
        private readonly IConfiguration _configuration;
        private readonly ITradeService _tradeService;

        public GetTradingAccountData(IConfiguration configuration, ITradeService tradeService, ISymbolItemRepository symbolRepo, ILadderItemRepository ladderRepo, IBlockClosedItemRepository blockClosedRepo, IBlockCondensedItemRepository blockCondensedRepo)
        {
            _configuration = configuration;
            _tradeService = tradeService;
        }

        [FunctionName("GetTradingAccountData")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();

            // Get account data
            var buyingPower = await _tradeService.GetBuyingPower(_configuration, userId);
            var accountValue = await _tradeService.GetAccountValue(_configuration, userId);
            var accountValuePreviousDay = await _tradeService.GetAccountValuePreviousDay(_configuration, userId);
            var accountData = new TradingAccountData { BuyingPower = buyingPower, AccountValue = accountValue, AccountValuePreviousDay = accountValuePreviousDay };

            return new OkObjectResult(JsonConvert.SerializeObject(accountData));
        }
    }
}
