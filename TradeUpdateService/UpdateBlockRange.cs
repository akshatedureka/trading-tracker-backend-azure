﻿using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradeUpdateService.Models;
using TradeUpdateService.Enums;
using Newtonsoft.Json;

namespace TradeUpdateService
{
    public class UpdateBlockRange : IUpdateBlockRange
    {
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _client;

        public UpdateBlockRange(IConfiguration configuration)
        {
            _configuration = configuration;
            var endpointUri = _configuration.GetValue<string>("EndPointUri"); // The Azure Cosmos DB endpoint
            var primaryKey = _configuration.GetValue<string>("PrimaryKey"); // The primary key for the Azure Cosmos account

            // Connect to Cosmos DB using endpoint
            _client = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
        }

        public async Task<bool> CreateUpdateBlockRangeMessage()
        {
            const string databaseId = "TMS";
            const string containerId = "Accounts";
            const string containerSymbolsId = "Symbols";

            var database = (Database)await _client.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/userId");
            var containerSymbols = (Container)await database.CreateContainerIfNotExistsAsync(containerSymbolsId, "/userId");

            var accounts = container
                .GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true).ToList();

            // Get the connection string from app settings
            var connectionString = _configuration.GetValue<string>("AzureWebJobsStorageRemote");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            var queueClient = new QueueClient(connectionString, "swingupdateblockrangequeue");
            await queueClient.CreateIfNotExistsAsync();

            foreach (var account in accounts.Where(account => account.AccountType == AccountTypes.SwingLong || account.AccountType == AccountTypes.SwingShort))
            {
                // Read symbols for user from Cosmos DB
                var userSymbolResponse = containerSymbols
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == account.UserId).ToList().FirstOrDefault();

                if (userSymbolResponse != null)
                {
                    var symbols = userSymbolResponse.Symbols;

                    if (symbols == null) continue;

                    foreach (var symbol in symbols.Where(s => s.Trading))
                    {
                        var msg = new UpdateBlockRangeMessage
                        {
                            UserId = account.UserId,
                            Symbol = symbol.Name
                        };

                        await queueClient.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                    }
                }
            }

            return true;
        }

        private static string Base64Encode(string plainText) // ToDo: Make this a common function
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}