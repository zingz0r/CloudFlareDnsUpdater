using System.Threading.Tasks;
using CloudFlare.Client;
using CloudFlare.Client.Api.Authentication;
using CloudFlare.Client.Interfaces;
using CloudFlareDnsUpdater.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudFlareDnsUpdater
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables();

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var cloudFlareConfig = hostContext.Configuration.GetSection("CloudFlare").Get<CloudFlareAuthentication>();
                    services.AddSingleton<ICloudFlareClient, CloudFlareClient>(s => new CloudFlareClient(cloudFlareConfig.Email, cloudFlareConfig.ApiKey));

                    services.AddHostedService<DnsUpdaterHostedService>();
                })
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });
            
            await builder.RunConsoleAsync();
        }
    }
}
