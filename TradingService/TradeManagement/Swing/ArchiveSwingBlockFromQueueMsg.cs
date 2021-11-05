using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.TradeManagement.Swing
{
    public class ArchiveSwingBlockFromQueueMsg
    {
        [FunctionName("ArchiveSwingBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("archiveswingblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            log.LogInformation($"ArchiveSwingBlockFromQueueMsg triggered.");

            var archiveBlockMessage = JsonConvert.DeserializeObject<ArchiveBlockMessage>(myQueueItem);
            log.LogInformation($"ArchiveSwingBlockFromQueueMsg triggered for user {archiveBlockMessage.UserId}, symbol {archiveBlockMessage.Symbol}, block id {archiveBlockMessage.BlockId}.");

            const string databaseId = "Tracker";
            const string containerId = "BlocksArchive";
            var container = await Repository.GetContainer(databaseId, containerId);

            var archiveBlock = new ArchiveBlock()
            {
                Id = Guid.NewGuid().ToString(),
                BlockId = archiveBlockMessage.BlockId,
                DateCreated = DateTime.Now,
                UserId = archiveBlockMessage.UserId,
                Symbol = archiveBlockMessage.Symbol,
                NumShares = archiveBlockMessage.NumShares,
                ExternalBuyOrderId = archiveBlockMessage.ExternalBuyOrderId,
                ExternalSellOrderId = archiveBlockMessage.ExternalSellOrderId,
                ExternalStopLossOrderId = archiveBlockMessage.ExternalStopLossOrderId,
                BuyOrderFilledPrice = archiveBlockMessage.BuyOrderFilledPrice,
                DateBuyOrderFilled = archiveBlockMessage.DateBuyOrderFilled,
                DateSellOrderFilled = archiveBlockMessage.DateSellOrderFilled,
                SellOrderFilledPrice = archiveBlockMessage.SellOrderFilledPrice,
                IsShort = false,
                Profit = (archiveBlockMessage.SellOrderFilledPrice - archiveBlockMessage.BuyOrderFilledPrice) * archiveBlockMessage.NumShares
            };

            await container.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
        }
    }
}
