using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace TradingService.Common.Repository
{
    public class Repository : IRepository
    {
        private readonly IConfiguration _configuration;

        public Repository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<Container> GetContainer(string containerId)
        {
            const string databaseId = "TMS";
            const string partitionKey = "userId";

            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKey);
            return container;
        }

        public async Task<Container> GetContainer(string containerId, string databaseId, string partitionKey)
        {
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKey);
            return container;
        }
    }
}