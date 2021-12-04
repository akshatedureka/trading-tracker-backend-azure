using System.Threading.Tasks;

namespace TradingService.Infrastructure.CosmosDbData.Interfaces
{
    public interface ICosmosDbContainerFactory
    {
        ICosmosDbContainer GetContainer(string containerName);

        Task EnsureDbSetupAsync();
    }
}
