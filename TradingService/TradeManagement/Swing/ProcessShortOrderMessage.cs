using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.TradeManagement.Swing.BusinessLogic;
using TradingService.TradeManagement.Swing.Models;

namespace TradingService.TradeManagement.Swing
{
    public class ProcessShortOrderMessage
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;
        private readonly ITradeManagementHelper _tradeManagementHelper;

        public ProcessShortOrderMessage(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order, ITradeManagementHelper tradeManagementHelper)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
            _tradeManagementHelper = tradeManagementHelper;
        }

        [FunctionName("ProcessShortOrderMessage")]
        public async Task Run([QueueTrigger("swingshortorderqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var message = JsonConvert.DeserializeObject<OrderMessage>(myQueueItem);
            var userId = message.UserId;
            var symbol = message.Symbol;
            var messageType = message.OrderMessageType;

            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(userId))
            {
                log.LogError("Required data is missing from the queue message.");
                throw new Exception("Required data is missing");
            }

            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            switch (messageType)
            {
                case Enums.OrderMessageTypes.Create:
                    try
                    {
                        await _tradeManagementHelper.CreateShortBracketOrdersBasedOnCurrentPrice(blocks, userId, symbol, log);
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error creating initial sell orders: {ex.Message}.");
                    }

                    log.LogInformation($"Successfully created sell orders for user {userId} symbol {symbol}.");
                    break;
                case Enums.OrderMessageTypes.Update:
                    if (message.OrderSide == OrderSide.Buy)
                    {
                        await _tradeManagementHelper.UpdateShortBuyOrderExecuted(userId, symbol, message.OrderId, message.ExecutedPrice, log);
                    }
                    else
                    {
                        await _tradeManagementHelper.UpdateShortSellOrderExecuted(userId, symbol, message.OrderId, message.ExecutedPrice, log);
                    }

                    log.LogInformation($"Successfully updated short order for user {userId} symbol {symbol}.");

                    break;
                default:
                    break;
            }

        }
    }
}