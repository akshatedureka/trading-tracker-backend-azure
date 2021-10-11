using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace TradeUpdates
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
            // ToDo: check for job already running for user before enqueue
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            const string databaseId = "Tracker";
            const string containerId = "Users";

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/userId");

            var users = container
                .GetItemLinqQueryable<Models.User>(allowSynchronousQueryExecution: true).ToList();

            foreach (var user in users)
            {
                if (_connectedUsers.Contains(user.UserId)) continue;

                var alpacaAPIKey = _configuration.GetValue<string>("AlpacaPaperAPIKey" + ":" + user.UserId); // ToDo: Set configuration refresh rate
                var alpacaAPISecret = _configuration.GetValue<string>("AlpacaPaperAPISec" + ":" + user.UserId);
                _backgroundJobClient.Enqueue<ITradeUpdateListener>(x => x.StartListening(user.UserId, alpacaAPIKey, alpacaAPISecret));
                _connectedUsers.Add(user.UserId);
            }
            
            return true;
        }
    }
}