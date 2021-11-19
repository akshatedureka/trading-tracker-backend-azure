using System.Threading.Tasks;
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

        public ResetSwingBlockFromQueueMsg(IQueries queries)
        {
            _queries = queries;
        }

        [FunctionName("ResetSwingBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("resetswingblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var resetBlockMessage = JsonConvert.DeserializeObject<ResetBlockMessage>(myQueueItem);
            log.LogInformation($"ResetSwingBlockFromQueueMsg triggered for user {resetBlockMessage.UserId}, symbol {resetBlockMessage.Symbol}, block id {resetBlockMessage.BlockId}.");

            await _queries.ResetUserBlockByUserIdAndSymbol(resetBlockMessage.UserId, resetBlockMessage.Symbol, resetBlockMessage.BlockId);
        }
    }
}
