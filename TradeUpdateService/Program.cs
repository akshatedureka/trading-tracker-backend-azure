using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TradeUpdateService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                    webBuilder.ConfigureAppConfiguration(config =>
                    {
                        var settings = config.Build();
                        var connection = settings.GetConnectionString("appConfiguration");
                        config.AddAzureAppConfiguration(connection);
                    }).UseStartup<Startup>());
    }
}