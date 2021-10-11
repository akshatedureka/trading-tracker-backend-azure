using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace TradingService.Common.Repository
{
    public static class Repository
    {
        // The Azure Cosmos DB endpoint for running this sample.
        private static readonly string EndpointUri = Environment.GetEnvironmentVariable("EndPointUri");

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

        // Connect to Cosmos DB using endpoint
        private static readonly CosmosClient CosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });

        public static async Task<Container> GetContainer(string databaseId, string containerId, string partitionKey = "userId")
        {
            var database = (Database)await CosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/" + partitionKey);
            return container;
        }
    }
}