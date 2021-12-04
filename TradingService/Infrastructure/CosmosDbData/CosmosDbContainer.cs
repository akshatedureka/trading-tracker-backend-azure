using Microsoft.Azure.Cosmos;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData
{
    public class CosmosDbContainer : ICosmosDbContainer
    {
        public Container _container { get; }

        public CosmosDbContainer(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }
    }
}
