using System;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.AppSettings;
using TradingService.Infrastructure.CosmosDbData.Repository;
using TradingService.Infrastructure.Extensions;
using TradingService.TradeManagement.Swing.BusinessLogic;

[assembly: FunctionsStartup(typeof(TradingService.Startup))]

namespace TradingService
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            var cs = Environment.GetEnvironmentVariable("appConfiguration");
            builder.ConfigurationBuilder.AddAzureAppConfiguration(cs);
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IRepository, Repository>();
            builder.Services.AddSingleton<IQueries, Queries>();
            builder.Services.AddScoped<ITradeOrder, TradeOrder>();
            builder.Services.AddScoped<ITradeManagementHelper, TradeManagementHelper>();

            var endpointUri = builder.GetContext().Configuration.GetValue<string>("EndPointUri"); // The Azure Cosmos DB endpoint
            var primaryKey = builder.GetContext().Configuration.GetValue<string>("PrimaryKey"); // The primary key for the Azure Cosmos account
            var dataBaseName = "TMS";
            var containers = new List<ContainerInfo> { new ContainerInfo { Name = "Symbols", PartitionKey = "UserId" } };

            builder.Services.AddCosmosDb(endpointUri, primaryKey, dataBaseName, containers);
            builder.Services.AddScoped<ISymbolItemRepository, SymbolItemRepository>();
            builder.Services.AddScoped<ILadderItemRepository, LadderItemRepository>();
        }
    }
}
