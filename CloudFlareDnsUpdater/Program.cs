using Autofac;
using Autofac.Extensions.DependencyInjection;
using CloudFlareDnsUpdater.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CloudFlareDnsUpdater
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            LogContext.PushProperty("SourceContext", "Main");

            var builder = new HostBuilder()
                .UseServiceProviderFactory(_ => new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration((_, config) =>
                {
                    config.AddJsonFile("appsettings.json", true);
                    config.AddEnvironmentVariables();

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddHttpClient<DnsUpdaterHostedService>().SetHandlerLifetime(TimeSpan.FromSeconds(5));
                })
                .ConfigureContainer<ContainerBuilder>((_, containerBuilder) =>
                {
                    containerBuilder.RegisterType<DnsUpdaterHostedService>().As<IHostedService>().SingleInstance();
                    containerBuilder.Register(c => c.Resolve<IHttpClientFactory>().CreateClient()).As<HttpClient>();
                })
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration));

            Log.Information("Running application");

            await builder.RunConsoleAsync();
        }
    }
}
