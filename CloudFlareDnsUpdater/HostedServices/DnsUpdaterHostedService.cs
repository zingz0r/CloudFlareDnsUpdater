using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CloudFlare.Client;
using CloudFlare.Client.Enumerators;
using CloudFlare.Client.Models;
using CloudFlareDnsUpdater.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudFlareDnsUpdater.HostedServices
{
    internal class DnsUpdaterHostedService : IHostedService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IAuthentication _authentication;

        public DnsUpdaterHostedService(HttpClient httpClient, ILogger<DnsUpdaterHostedService> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _authentication = new ApiKeyAuthentication(config.GetValue<string>("CloudFlare.Email"), config.GetValue<string>("CloudFlare.ApiKey"));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[{DateTime.UtcNow} | Info] Started DNS updater...");

            while(!cancellationToken.IsCancellationRequested)
            {
                await UpdateDnsAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        public async Task UpdateDnsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new CloudFlareClient(_authentication))
                {
                    var externalIpAddress = await GetIpAddressAsync(cancellationToken);
                    
                    if (externalIpAddress == null)
                    {
                        _logger.LogError($"[{DateTime.UtcNow} | Error] External IP is null.");

                        return;
                    }

                    var zones = (await client.GetZonesAsync(cancellationToken)).Result;

                    foreach (var zone in zones)
                    {
                        var records = (await client.GetDnsRecordsAsync(zone.Id, DnsRecordType.A, cancellationToken)).Result;
                        foreach (var record in records)
                        {
                            if (record.Type == DnsRecordType.A && record.Content != externalIpAddress.ToString())
                            {
                                var updateResult = (await client.UpdateDnsRecordAsync(zone.Id, record.Id,
                                    DnsRecordType.A, record.Name, externalIpAddress.ToString(), cancellationToken));

                                if (!updateResult.Success)
                                {
                                    foreach (var error in updateResult.Errors)
                                    {
                                        _logger.LogError($"[{DateTime.UtcNow} | Error] {{{record.Name}}} {error.Message}");
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        $"[{DateTime.UtcNow} | Update] {{{record.Name}}} {record.Content} -> {externalIpAddress}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private async Task<IPAddress> GetIpAddressAsync(CancellationToken cancellationToken)
        {
            IPAddress ipAddress = null;
            foreach (var provider in ExternalIpProviders.Providers)
            {
                if (ipAddress != null)
                {
                    break;
                }
                               
                var response = await _httpClient.GetAsync(provider, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var ip = await response.Content.ReadAsStringAsync(cancellationToken);
                    Regex.Replace(ip, @"\t|\n|\r", "");
                    ipAddress = IPAddress.Parse(ip);
                }
            }

            return ipAddress;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
