using System.IO;
using System.Reflection;
using System.Text.Json;

namespace THBIM.AutoUpdate.Helpers;

/// <summary>
/// Resolves the license server endpoint URL.
///
/// Resolution order:
///   1. Environment variable THBIM_LICENSE_ENDPOINT
///   2. JSON file `license-endpoint.json` next to the executable
///   3. Embedded fallback
///
/// JSON format: { "endpoint": "https://..." }
///
/// Rationale: hard-coding the URL in compiled IL exposes it on every decompile
/// and makes endpoint rotation impossible without shipping a new build.
/// </summary>
internal static class LicenseEndpointConfig
{
    private const string EnvVarName = "THBIM_LICENSE_ENDPOINT";
    private const string ConfigFileName = "license-endpoint.json";

    // Embedded fallback split across two strings — removes the URL from naive
    // `strings *.exe` scans. Not a security boundary.
    private const string FallbackPart1 =
        "https://script.google.com/macros/s/";
    private const string FallbackPart2 =
        "AKfycbxbMI884nGWzL6u7p10ChLqzOHSl4KXpJvr9EcsYP0lMiE3_ZNchDmNsDtLhOLEtKiYCw/exec";

    private static string? _cached;

    public static string Resolve()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached;

        try
        {
            var fromEnv = Environment.GetEnvironmentVariable(EnvVarName);
            if (!string.IsNullOrWhiteSpace(fromEnv) && IsValidHttpsUrl(fromEnv))
            {
                _cached = fromEnv.Trim();
                return _cached;
            }
        }
        catch { /* ignore */ }

        try
        {
            // AppContext.BaseDirectory works correctly for both regular and
            // single-file (PublishSingleFile=true) deployments. ProcessPath
            // is the second-best fallback (also reliable for single-file).
            // Assembly.Location is intentionally avoided — it returns empty
            // string when the assembly is embedded in a single-file app.
            var exeDir = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(exeDir))
                exeDir = Path.GetDirectoryName(Environment.ProcessPath);

            if (!string.IsNullOrWhiteSpace(exeDir))
            {
                var candidate = Path.Combine(exeDir, ConfigFileName);
                var endpoint = TryReadEndpoint(candidate);
                if (!string.IsNullOrWhiteSpace(endpoint) && IsValidHttpsUrl(endpoint))
                {
                    _cached = endpoint.Trim();
                    return _cached;
                }
            }
        }
        catch { /* ignore */ }

        _cached = FallbackPart1 + FallbackPart2;
        return _cached;
    }

    private static string? TryReadEndpoint(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("endpoint", out var prop))
                return prop.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

    // HTTPS-only — refuse any plain HTTP override. An attacker with write
    // access to env vars or the executable directory could otherwise downgrade
    // the channel and capture credentials in the clear.
    private static bool IsValidHttpsUrl(string s)
    {
        return Uri.TryCreate(s.Trim(), UriKind.Absolute, out var u)
            && u.Scheme == Uri.UriSchemeHttps;
    }
}
