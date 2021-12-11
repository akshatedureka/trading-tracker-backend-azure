using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Models;
using Azure.Storage.Queues;

namespace TradingService.Functions.BlockManagement
{
    public class UpdateBlockRangeQueueMsg
    {
        private readonly IConfiguration _configuration;
        private readonly IAccountItemRepository _accountRepo;
        private readonly ILadderItemRepository _ladderRepo;

        public UpdateBlockRangeQueueMsg(IConfiguration configuration, IAccountItemRepository accountRepo, ILadderItemRepository ladderRepo)
        {
            _configuration = configuration;
            _accountRepo = accountRepo;
            _ladderRepo = ladderRepo;
        }

        [FunctionName("UpdateBlockRangeQueueMsg")]
        public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, ILogger log) // Every hour
        {
            var accounts = await _accountRepo.GetItemsAsync();

            // Get the connection string from app settings
            var connectionString = _configuration.GetValue<string>("AzureWebJobsStorageRemote");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            var queueClient = new QueueClient(connectionString, "updateblockrangequeue");
            await queueClient.CreateIfNotExistsAsync();

            foreach (var account in accounts)
            {
                // Read ladders for user from Cosmos DB to check if blocks have been created
                var userLadderRepsone = await _ladderRepo.GetItemsAsyncByUserId(account.UserId);

                if (userLadderRepsone != null)
                {
                    var ladders = userLadderRepsone.FirstOrDefault().Ladders;

                    if (ladders == null) continue;

                    foreach (var ladder in ladders.Where(l => l.BlocksCreated))
                    {
                        var msg = new UpdateBlockRangeMessage
                        {
                            UserId = account.UserId,
                            Symbol = ladder.Symbol
                        };

                        await queueClient.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                        log.LogInformation($"Added message to queue to update block range for user {msg.UserId} and symbol {msg.Symbol} at {DateTime.Now}.");
                    }
                }
            }
        }

        private string Base64Encode(string plainText) // ToDo: Make this a common function
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
