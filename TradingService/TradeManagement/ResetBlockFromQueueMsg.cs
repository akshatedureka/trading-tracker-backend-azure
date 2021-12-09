using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.TradeManagement
{
    public class ResetBlockFromQueueMsg
    {
        private readonly IQueries _queries;

        public ResetBlockFromQueueMsg(IQueries queries)
        {
            _queries = queries;
        }

        [FunctionName("ResetBlockFromQueueMsg")]
        public async Task Run([QueueTrigger("resetblockqueue", Connection = "AzureWebJobsStorageRemote")] string myQueueItem, ILogger log)
        {
            var resetBlockMessage = JsonConvert.DeserializeObject<ResetBlockMessage>(myQueueItem);
            log.LogInformation($"ResetBlockFromQueueMsg triggered for user {resetBlockMessage.UserId}, symbol {resetBlockMessage.Symbol}, block id {resetBlockMessage.BlockId}.");

            //await _queries.ResetUserBlockByUserIdAndSymbol(resetBlockMessage.UserId, resetBlockMessage.Symbol, resetBlockMessage.BlockId);
        }
    }
}
