using System;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Core.Interfaces.Persistence;
using TradingService.TradeManagement.BusinessLogic;
using TradingService.TradeManagement.Models;

namespace TradingService.TradeManagement
{
    public class ProcessShortOrderMessage
    {
        private readonly ITradeManagementHelper _tradeManagementHelper;
        private readonly IBlockItemRepository _blockRepo;

        public ProcessShortOrderMessage(IBlockItemRepository blockRepo, ITradeManagementHelper tradeManagementHelper)
        {
            _blockRepo = blockRepo;
            _tradeManagementHelper = tradeManagementHelper;
        }

        [FunctionName("ProcessShortOrderMessage")]
        public async Task Run([QueueTrigger("shortorderqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
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

            var blocks = await _blockRepo.GetItemsAsyncByUserIdAndSymbol(userId, symbol);

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
