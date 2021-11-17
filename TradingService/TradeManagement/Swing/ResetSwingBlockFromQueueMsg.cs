using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.TradeManagement.Swing
{
    public class ResetSwingBlockFromQueueMsg
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public ResetSwingBlockFromQueueMsg(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
        }

        [FunctionName("ResetSwingBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("resetswingblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            log.LogInformation($"ResetSwingBlockFromQueueMsg triggered.");

            var resetBlockMessage = JsonConvert.DeserializeObject<ResetBlockMessage>(myQueueItem);
            log.LogInformation($"ResetSwingBlockFromQueueMsg triggered for user {resetBlockMessage.UserId}, symbol {resetBlockMessage.Symbol}, block id {resetBlockMessage.BlockId}.");

            const string containerId = "Blocks";
            var container = await _repository.GetContainer(containerId);

            var blocks = await _queries.GetBlocksByUserIdAndSymbol(resetBlockMessage.UserId, resetBlockMessage.Symbol);
            var blockToReset = blocks.FirstOrDefault(b => b.Id == resetBlockMessage.BlockId);

            blockToReset.ExternalBuyOrderId = new Guid();
            blockToReset.ExternalSellOrderId = new Guid();
            blockToReset.ExternalStopLossOrderId = new Guid();
            blockToReset.BuyOrderCreated = false;
            blockToReset.BuyOrderFilled = false;
            blockToReset.BuyOrderFilledPrice = 0;
            blockToReset.DateBuyOrderFilled = DateTime.MinValue;
            blockToReset.SellOrderCreated = false;
            blockToReset.SellOrderFilled = false;
            blockToReset.SellOrderFilledPrice = 0;
            blockToReset.DateSellOrderFilled = DateTime.MinValue;

            await container.ReplaceItemAsync(blockToReset, blockToReset.Id, new PartitionKey(blockToReset.UserId));
        }
    }
}
