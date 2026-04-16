using System.Text.RegularExpressions;

namespace strAppersBackend.Utilities;

/// <summary>Strips common API credential query parameters from URLs before logging.</summary>
public static class HttpUrlQuerySecretRedaction
{
    private static readonly Regex QueryCredentialParams = new(
        @"([?&])(key|token|access_token|refresh_token|client_secret)=([^&]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    public static string Redact(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return url ?? string.Empty;
        return QueryCredentialParams.Replace(url, m => $"{m.Groups[1].Value}{m.Groups[2].Value}=[redacted]");
    }
}
