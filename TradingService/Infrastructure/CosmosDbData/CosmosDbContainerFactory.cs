using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingService.Infrastructure.AppSettings;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData
{
    public class CosmosDbContainerFactory : ICosmosDbContainerFactory
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;
        private readonly List<ContainerInfo> _containers;

        public CosmosDbContainerFactory(CosmosClient cosmosClient, string databaseName, List<ContainerInfo> containers)
        {
            _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            _containers = containers ?? throw new ArgumentNullException(nameof(containers));
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        }

        public ICosmosDbContainer GetContainer(string containerName)
        {
            if (_containers.Where(x => x.Name == containerName) == null)
            {
                throw new ArgumentException($"Unable to find container: {containerName}");
            }

            return new CosmosDbContainer(_cosmosClient, _databaseName, containerName);
        }

        public async Task EnsureDbSetupAsync()
        {
            DatabaseResponse database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);

            foreach (ContainerInfo container in _containers)
            {
                await database.Database.CreateContainerIfNotExistsAsync(container.Name, $"{container.PartitionKey}");
            }
        }
    }
}
