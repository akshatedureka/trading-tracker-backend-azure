using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Models;

namespace TradingService.Functions.TradeManagement
{
    public class CloseBlockFromQueueMsg
    {
        private readonly IBlockClosedItemRepository _blockClosedRepo;

        public CloseBlockFromQueueMsg(IBlockClosedItemRepository blockClosedRepo)
        {
            _blockClosedRepo = blockClosedRepo;
        }

        [FunctionName("CloseBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("closeblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var closeBlockMessage = JsonConvert.DeserializeObject<ClosedBlockMessage>(myQueueItem);
            log.LogInformation($"CloseBlockFromQueueMsg triggered for user {closeBlockMessage.UserId}, symbol {closeBlockMessage.Symbol}, block id {closeBlockMessage.BlockId}.");

            var closedBlock = new ClosedBlock()
            {
                BlockId = closeBlockMessage.BlockId,
                UserId = closeBlockMessage.UserId,
                Symbol = closeBlockMessage.Symbol,
                NumShares = closeBlockMessage.NumShares,
                ExternalBuyOrderId = closeBlockMessage.ExternalBuyOrderId,
                ExternalSellOrderId = closeBlockMessage.ExternalSellOrderId,
                ExternalStopLossOrderId = closeBlockMessage.ExternalStopLossOrderId,
                BuyOrderFilledPrice = closeBlockMessage.BuyOrderFilledPrice,
                DateBuyOrderFilled = closeBlockMessage.DateBuyOrderFilled,
                DateSellOrderFilled = closeBlockMessage.DateSellOrderFilled,
                SellOrderFilledPrice = closeBlockMessage.SellOrderFilledPrice,
                IsShort = closeBlockMessage.DateBuyOrderFilled > closeBlockMessage.DateSellOrderFilled,
                Profit = (closeBlockMessage.SellOrderFilledPrice - closeBlockMessage.BuyOrderFilledPrice) * closeBlockMessage.NumShares
            };

            await _blockClosedRepo.AddItemAsync(closedBlock);
        }
    }
}
