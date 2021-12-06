using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities.Base;
using TradingService.Infrastructure.CosmosDbData.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Linq;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public abstract class CosmosDbRepository<T> : IRepository<T>, IContainerContext<T> where T : BaseEntity
    {
        public abstract string ContainerName { get; }

        public abstract string GenerateId(T entity);

        public abstract PartitionKey ResolvePartitionKey(string userId);

        private readonly ICosmosDbContainerFactory _cosmosDbContainerFactory;

        private readonly Container _container;

        public CosmosDbRepository(ICosmosDbContainerFactory cosmosDbContainerFactory)
        {
            _cosmosDbContainerFactory = cosmosDbContainerFactory ?? throw new ArgumentNullException(nameof(ICosmosDbContainerFactory));
            _container = _cosmosDbContainerFactory.GetContainer(ContainerName)._container;
        }

        public async Task<T> AddItemAsync(T item)
        {
            item.Id = GenerateId(item);
            item.DateCreated = DateTime.Now;
            var newItem = await _container.CreateItemAsync<T>(item, ResolvePartitionKey(item.UserId));

            return newItem;
        }

        public Task DeleteItemAsync(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<T> GetItemAsync(string id)
        {
            try
            {
                ItemResponse<T> response = await _container.ReadItemAsync<T>(id, ResolvePartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<T> UpdateItemAsync(T item)
        {
            var updatedItem = await _container.UpsertItemAsync<T>(item, ResolvePartitionKey(item.UserId));
            return updatedItem;
        }

        public async Task<List<T>> GetItemsAsyncByUserId(string userId)
        {
            string query = @$"SELECT * FROM c WHERE c.userId = @UserId";
            QueryDefinition queryDefinition = new QueryDefinition(query).WithParameter("@UserId", userId);

            FeedIterator<T> resultSetIterator = _container.GetItemQueryIterator<T>(queryDefinition);
            List<T> results = new List<T>();
            while (resultSetIterator.HasMoreResults)
            {
                FeedResponse<T> response = await resultSetIterator.ReadNextAsync();

                results.AddRange(response.ToList());
            }

            return results;
        }
    }
}
