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
    public class CloseSwingBlockFromQueueMsg
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public CloseSwingBlockFromQueueMsg(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
        }

        [FunctionName("CloseSwingBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("closeswingblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var closeBlockMessage = JsonConvert.DeserializeObject<ClosedBlockMessage>(myQueueItem);
            log.LogInformation($"CloseSwingBlockFromQueueMsg triggered for user {closeBlockMessage.UserId}, symbol {closeBlockMessage.Symbol}, block id {closeBlockMessage.BlockId}.");

            const string containerId = "BlocksClosed";
            var container = await _repository.GetContainer(containerId);

            var closedBlock = new ClosedBlock()
            {
                Id = Guid.NewGuid().ToString(),
                BlockId = closeBlockMessage.BlockId,
                DateCreated = DateTime.Now,
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

            await container.CreateItemAsync(closedBlock, new PartitionKey(closedBlock.UserId));
        }
    }
}
