using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Hangfire.MemoryStorage;

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
                .UseMemoryStorage());
            services.AddHangfireServer();
            services.AddSingleton<IConnectUsers, ConnectUsers>();
            services.AddTransient<ITradeUpdateListener, TradeUpdateListener>(); //ToDo: Singleton or transient?
            services.AddTransient<IDayTrading, DayTrading>(); //ToDo: Singleton or transient?
            services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IBackgroundJobClient backgroundJobClient)
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

            RecurringJob.AddOrUpdate<IConnectUsers>(x => x.GetUsersToConnect(), Cron.Minutely);
            RecurringJob.AddOrUpdate<IDayTrading>(x => x.TriggerDayTrades(), Cron.Minutely);
        }
    }
}
