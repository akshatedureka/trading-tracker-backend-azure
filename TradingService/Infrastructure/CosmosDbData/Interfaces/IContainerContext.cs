using Microsoft.Azure.Cosmos;
using TradingService.Core.Entities.Base;

namespace TradingService.Infrastructure.CosmosDbData.Interfaces
{
    public interface IContainerContext<T> where T : BaseEntity
    {
        string ContainerName { get; }
        string GenerateId(T entity);
        PartitionKey ResolvePartitionKey(string userId);
    }
}
