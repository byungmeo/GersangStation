using Core.Models;
using System;

namespace GersangStation.Services;

internal static class BrowserAddressResolver
{
    internal static Uri Resolve(string input, BrowserSearchProvider provider)
    {
        input = input.Trim();
        if (input.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            return new Uri("about:blank");

        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(absolute.Host))
            return absolute;

        // A port is allowed for a bare host, but arbitrary URI schemes are not host names.
        if (!input.Contains("://", StringComparison.Ordinal) && !input.Contains(' ')
            && Uri.TryCreate("https://" + input, UriKind.Absolute, out Uri? host)
            && string.IsNullOrEmpty(host.UserInfo)
            && (host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                || host.Host.Contains('.')))
            return host;

        string endpoint = provider switch
        {
            BrowserSearchProvider.Bing => "https://www.bing.com/search?q=",
            BrowserSearchProvider.Naver => "https://search.naver.com/search.naver?query=",
            BrowserSearchProvider.Daum => "https://search.daum.net/search?q=",
            _ => "https://www.google.com/search?q="
        };
        return new Uri(endpoint + Uri.EscapeDataString(input));
    }
}
