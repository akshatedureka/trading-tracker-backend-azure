using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Hangfire.MemoryStorage;
using System;

namespace TradeUpdateService
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseDefaultTypeSerializer()
                .UseMemoryStorage(new MemoryStorageOptions { FetchNextJobTimeout = TimeSpan.FromDays(1) })); // Must set or else jobs will reprocess automatically after 30 minutes; If using db option, need to set invisibility timeout property
            services.AddHangfireServer(options => { options.WorkerCount = Environment.ProcessorCount * 10; }); // Max workers
            services.AddSingleton<IConnectUsers, ConnectUsers>();
            services.AddScoped<ITradeUpdateListener, TradeUpdateListener>();
            services.AddSingleton<ICreateOrders, CreateOrders>();
            services.AddSingleton<IUpdateBlockRange, UpdateBlockRange>();
            services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });

            app.UseHangfireDashboard();

            BackgroundJob.Enqueue<IConnectUsers>(x => x.GetUsersToConnect()); // run immediately, then on a schedule to check for new users
            RecurringJob.AddOrUpdate<IConnectUsers>(x => x.GetUsersToConnect(), Cron.Minutely);
            RecurringJob.AddOrUpdate<ICreateOrders>(x => x.CreateBuySellOrders(), "*/30 * * * * *"); // every 30 seconds
            RecurringJob.AddOrUpdate<IUpdateBlockRange>(x => x.CreateUpdateBlockRangeMessage(), Cron.Hourly);
        }
    }
}
