using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using TradingService.Common.Models;

namespace TradingService.TradeManagement.Swing.Common
{
    public class TradeManagementCommon
    {
        public static async Task CreateClosedBlockMsg(ILogger log, IConfiguration config, UserBlock userBlock, Block block)
        {
            // Place an closed block msg on the queue
            var connectionString = config.GetValue<string>("AzureWebJobsStorageRemote");
            var queueName = "closeswingblockqueue";
            var queueClient = new QueueClient(connectionString, queueName);
            queueClient.CreateIfNotExists();

            var msg = new ClosedBlockMessage()
            {
                BlockId = block.Id,
                UserId = userBlock.UserId,
                Symbol = userBlock.Symbol,
                NumShares = userBlock.NumShares,
                ExternalBuyOrderId = block.ExternalBuyOrderId,
                ExternalSellOrderId = block.ExternalSellOrderId,
                ExternalStopLossOrderId = block.ExternalStopLossOrderId,
                BuyOrderFilledPrice = block.BuyOrderFilledPrice,
                DateBuyOrderFilled = block.DateBuyOrderFilled,
                DateSellOrderFilled = block.DateSellOrderFilled,
                SellOrderFilledPrice = block.SellOrderFilledPrice
            };

            await queueClient.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
            log.LogInformation($"Created closed block queue msg for user {userBlock.UserId}, block id {block.Id} at: { DateTimeOffset.Now}.");
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
