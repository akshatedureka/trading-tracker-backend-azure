﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradeUpdateService.Models;

namespace TradeUpdateService
{
    public class ConnectUsers : IConnectUsers
    {
        private readonly IConfiguration _configuration;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly List<string> _connectedUsers;

        public ConnectUsers(IConfiguration configuration, IBackgroundJobClient backgroundJobClient)
        {
            _configuration = configuration;
            _backgroundJobClient = backgroundJobClient;
            _connectedUsers = new List<string>();
        }

        public async Task<bool> GetUsersToConnect()
        {
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            const string databaseId = "Tracker";
            const string containerId = "Accounts";

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/userId");

            var accounts = container
                .GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true).ToList();

            foreach (var account in accounts)
            {
                if (_connectedUsers.Contains(account.UserId) || !account.HasEnteredKeys) continue;

                var alpacaAPIKey = _configuration.GetValue<string>("AlpacaPaperAPIKey" + ":" + account.UserId); // ToDo: Set configuration refresh rate
                var alpacaAPISecret = _configuration.GetValue<string>("AlpacaPaperAPISec" + ":" + account.UserId);
                _backgroundJobClient.Enqueue<ITradeUpdateListener>(x => x.StartListening(account.UserId, account.AccountType, alpacaAPIKey, alpacaAPISecret));
                _connectedUsers.Add(account.UserId);
            }
            
            return true;
        }
    }
}