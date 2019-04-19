using System.Collections.Generic;

namespace CloudFlareDnsUpdater.Providers
{
    public static class ExternalIpProviders
    {
        public static IEnumerable<string> Providers { get; }

        static ExternalIpProviders()
        {
            Providers = new List<string>
            {
                "http://ipecho.net/plain",
                "http://icanhazip.com/",
                "http://whatismyip.akamai.com",
                "https://tnx.nl/ip"
            };
        }
    }
}