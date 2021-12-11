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
using TradingService.Core.Enums;

namespace TradingService.Functions.TradeManagement
{
    public class CreateOrdersQueueMsg
    {
        private readonly IConfiguration _configuration;
        private readonly ISymbolItemRepository _symbolRepo;
        private readonly IAccountItemRepository _accountRepo;

        public CreateOrdersQueueMsg(IConfiguration configuration, ISymbolItemRepository symbolRepo, IAccountItemRepository accountRepo)
        {
            _configuration = configuration;
            _symbolRepo = symbolRepo;
            _accountRepo = accountRepo;
        }

        [FunctionName("CreateOrdersQueueMsg")]
        public async Task Run([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer, ILogger log) // Every hour
        {
            // Get the connection string from app settings
            var connectionString = _configuration.GetValue<string>("AzureWebJobsStorageRemote");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            var queueClientLong = new QueueClient(connectionString, "longorderqueue");
            var queueClientShort = new QueueClient(connectionString, "shortorderqueue");
            await queueClientLong.CreateIfNotExistsAsync();
            await queueClientShort.CreateIfNotExistsAsync();

            var accounts = await _accountRepo.GetItemsAsync();

            foreach (var account in accounts)
            {
                // Read symbols for user from Cosmos DB
                var userSymbolResponse = await _symbolRepo.GetItemsAsyncByUserId(account.UserId);

                if (userSymbolResponse != null)
                {
                    var symbols = userSymbolResponse.FirstOrDefault().Symbols;

                    if (symbols == null) continue;

                    foreach (var symbol in symbols.Where(s => s.Trading))
                    {
                        var msg = new OrderMessage
                        {
                            UserId = account.UserId,
                            Symbol = symbol.Name,
                            OrderMessageType = OrderMessageTypes.Create
                        };

                        if (account.AccountType == AccountTypes.Long)
                        {
                            await queueClientLong.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                        }
                        else
                        {
                            await queueClientShort.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                        }
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
