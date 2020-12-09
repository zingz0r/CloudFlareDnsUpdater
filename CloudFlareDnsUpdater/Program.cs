using System;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using CloudFlareDnsUpdater.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;

namespace CloudFlareDnsUpdater
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            LogContext.PushProperty("SourceContext", "Main");

            var builder = new HostBuilder()
                .UseServiceProviderFactory(context => new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables();

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostingContext, services) => {
                    services.AddHttpClient<DnsUpdaterHostedService>().SetHandlerLifetime(TimeSpan.FromSeconds(5));
                })
                .ConfigureContainer<ContainerBuilder>((hostingContext, builder) => {
                    builder.RegisterType<DnsUpdaterHostedService>().As<IHostedService>().SingleInstance();
                    builder.Register(c => c.Resolve<IHttpClientFactory>().CreateClient()).As<HttpClient>();
                })
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration));

            Log.Information("Running application");

            await builder.RunConsoleAsync();
        }
    }
}
