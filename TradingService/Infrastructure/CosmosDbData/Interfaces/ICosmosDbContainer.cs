using Microsoft.Azure.Cosmos;

namespace TradingService.Infrastructure.CosmosDbData.Interfaces
{
    public interface ICosmosDbContainer
    {
        Container _container { get; }
    }
}
