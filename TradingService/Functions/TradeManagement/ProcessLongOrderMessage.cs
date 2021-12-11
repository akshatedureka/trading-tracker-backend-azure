using System;
using System.Threading.Tasks;
using Alpaca.Markets;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Core.Models;
using TradingService.Core.Enums;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.Helpers.Interfaces;

namespace TradingService.Functions.TradeManagement
{
    public class ProcessLongOrderMessage
    {
        private readonly ITradeManagementHelper _tradeManagementHelper;
        private readonly IBlockItemRepository _blockRepo;

        public ProcessLongOrderMessage(IBlockItemRepository blockRepo, ITradeManagementHelper tradeManagementHelper)
        {
            _blockRepo = blockRepo;
            _tradeManagementHelper = tradeManagementHelper;
        }

        [FunctionName("ProcessLongOrderMessage")]
        public async Task Run([QueueTrigger("longorderqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
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
                case OrderMessageTypes.Create:
                    try
                    {
                        await _tradeManagementHelper.CreateLongBracketOrdersBasedOnCurrentPrice(blocks, userId, symbol, log);
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error creating initial buy orders: {ex.Message}.");
                    }

                    log.LogInformation($"Successfully created buy orders for user {userId} symbol {symbol}.");

                    break;
                case OrderMessageTypes.Update:
                    if (message.OrderSide == OrderSide.Buy)
                    {
                        await _tradeManagementHelper.UpdateLongBuyOrderExecuted(userId, symbol, message.OrderId, message.ExecutedPrice, log);
                    }
                    else
                    {
                        await _tradeManagementHelper.UpdateLongSellOrderExecuted(userId, symbol, message.OrderId, message.ExecutedPrice, log);
                    }

                    log.LogInformation($"Successfully updated long order for user {userId} symbol {symbol}.");

                    break;
                default:
                    break;
            }
        }
    }
}
