﻿using CloudFlare.Client;
using CloudFlare.Client.Api.Authentication;
using CloudFlare.Client.Api.Zones.DnsRecord;
using CloudFlare.Client.Enumerators;
using CloudFlareDnsUpdater.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CloudFlareDnsUpdater.HostedServices
{
    internal class DnsUpdaterHostedService : IHostedService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IAuthentication _authentication;
        private readonly TimeSpan _updateInterval;

        public DnsUpdaterHostedService(HttpClient httpClient, ILogger logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger.ForContext<DnsUpdaterHostedService>();

            var token = config.GetValue<string>("CloudFlare:ApiToken");
            var email = config.GetValue<string>("CloudFlare:Email");
            var key = config.GetValue<string>("CloudFlare:ApiKey");

            _authentication = !string.IsNullOrEmpty(token)
                ? new ApiTokenAuthentication(token)
                : new ApiKeyAuthentication(email, key);
            _updateInterval = TimeSpan.FromSeconds(config.GetValue("UpdateIntervalSeconds", 30));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Started DNS updater...");

            while (!cancellationToken.IsCancellationRequested)
            {
                await UpdateDnsAsync(cancellationToken);
                _logger.Debug("Finished update process. Waiting '{@UpdateInterval}' for next check", _updateInterval);

                await Task.Delay(_updateInterval, cancellationToken);
            }
        }

        public async Task UpdateDnsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var client = new CloudFlareClient(_authentication);
                var externalIpAddress = await GetIpAddressAsync(cancellationToken);
                _logger.Debug("Got ip from external provider: {IP}", externalIpAddress?.ToString());

                if (externalIpAddress == null)
                {
                    _logger.Error("All external IP providers failed to resolve the IP");
                    return;
                }

                var zones = (await client.Zones.GetAsync(cancellationToken: cancellationToken)).Result;
                _logger.Debug("Found the following zones : {@Zones}", zones.Select(x => x.Name));

                foreach (var zone in zones)
                {
                    var records = (await client.Zones.DnsRecords.GetAsync(zone.Id,
                        new DnsRecordFilter {Type = DnsRecordType.A}, null, cancellationToken)).Result;
                    _logger.Debug("Found the following 'A' records in zone '{Zone}': {@Records}", zone.Name, records.Select(x => x.Name));

                    foreach (var record in records)
                    {
                        if (record.Type == DnsRecordType.A && record.Content != externalIpAddress.ToString())
                        {
                            var modified = new ModifiedDnsRecord
                            {
                                Type = DnsRecordType.A,
                                Name = record.Name,
                                Content = externalIpAddress.ToString(),
                            };
                            var updateResult =
                                (await client.Zones.DnsRecords.UpdateAsync(zone.Id, record.Id, modified,
                                    cancellationToken));

                            if (!updateResult.Success)
                            {
                                _logger.Error(
                                    "The following errors happened during update of record '{Record}' in zone '{Zone}': {@Error}",
                                    record.Name, zone.Name, updateResult.Errors);
                            }
                            else
                            {
                                _logger.Information(
                                    "Successfully updated record '{Record}' ip from '{PreviousIp}' to '{ExternalIpAddress}' in zone '{Zone}'",
                                    record.Name, record.Content, externalIpAddress.ToString(), zone.Name);
                            }
                        }
                        else
                        {
                            _logger.Debug(
                                "The IP for record '{Record}' in zone '{Zone}' is already '{ExternalIpAddress}'",
                                record.Name, zone.Name, externalIpAddress.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected exception happened");
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
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var ip = await response.Content.ReadAsStringAsync(cancellationToken);
                Regex.Replace(ip, @"\t|\n|\r", string.Empty);
                ipAddress = IPAddress.Parse(ip);
            }

            return ipAddress;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
