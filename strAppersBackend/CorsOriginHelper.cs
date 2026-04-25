using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace strAppersBackend;

/// <summary>
/// Shared rules for CORS and for attaching CORS headers to error responses.
/// </summary>
internal static class CorsOriginHelper
{
    public static IReadOnlyList<string> GetExtraOrigins(IConfiguration config)
    {
        var fromConfig = config.GetSection("Cors:ExtraAllowedOrigins").Get<string[]>();
        if (fromConfig == null || fromConfig.Length == 0) return Array.Empty<string>();
        return fromConfig
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray();
    }

    /// <param name="apiBaseForCors">Same as ApiBaseUrl in appsettings — used to allow same host as the API (e.g. Swagger).</param>
    public static bool IsOriginAllowed(
        string? origin,
        Uri? apiBaseForCors,
        IReadOnlyList<string> extraAllowedOrigins)
    {
        if (string.IsNullOrEmpty(origin)) return false;

        // Always allow local frontend dev origins (common Vite/React ports).
        if (origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
            origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(origin, "http://localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(origin, "http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var uri = new Uri(origin);

            if (apiBaseForCors != null && uri.Host.Equals(apiBaseForCors.Host, StringComparison.OrdinalIgnoreCase))
                return true;

            if (uri.Host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase))
                return true;

            if (uri.Host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase))
                return true;

            var allowedHosts = new[]
            {
                "preview--skill-in-ce9dcf39.base44.app",
                "skill-in.com",
                "localhost",
                "127.0.0.1",
                "20.126.90.3"
            };

            if (allowedHosts.Any(allowed =>
                    uri.Host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (extraAllowedOrigins.Count > 0 &&
                extraAllowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
