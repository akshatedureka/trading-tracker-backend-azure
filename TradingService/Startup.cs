using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingService.Common.Order;
using TradingService.Common.Repository;

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

        }
    }
}
