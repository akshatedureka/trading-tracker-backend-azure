using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using TradingService.Infrastructure.AppSettings;
using TradingService.Infrastructure.CosmosDbData;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddCosmosDb(this IServiceCollection services, string endpointUrl, string primaryKey, string databaseName, List<ContainerInfo> containers)
        {
            var client = new CosmosClient(endpointUrl, primaryKey);
            var cosmosDbClientFactory = new CosmosDbContainerFactory(client, databaseName, containers);

            // Microsoft recommends a singleton client instance to be used throughout the application
            // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclient?view=azure-dotnet#definition
            // "CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime of the application which enables efficient connection management and performance"
            services.AddSingleton<ICosmosDbContainerFactory>(cosmosDbClientFactory);

            return services;
        }
    }
}
