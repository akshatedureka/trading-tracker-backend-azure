using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace TradingService.Common.Repository
{
    public class Repository : IRepository
    {
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _client;

        public Repository(IConfiguration configuration)
        {
            _configuration = configuration;

            var endpointUri = _configuration.GetValue<string>("EndPointUri"); // The Azure Cosmos DB endpoint
            var primaryKey = _configuration.GetValue<string>("PrimaryKey"); // The primary key for the Azure Cosmos account

            _client = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
        }

        public async Task<Container> GetContainer(string containerId)
        {
            const string databaseId = "TMS";
            const string partitionKey = "userId";

            var database = (Database)await _client.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKey);
            return container;
        }

        public async Task<Container> GetContainer(string containerId, string databaseId, string partitionKey)
        {
            var database = (Database)await _client.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKey);
            return container;
        }
    }
}