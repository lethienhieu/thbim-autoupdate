namespace THBIM.AutoUpdate.Helpers;

public static class VersionComparer
{
    public static bool IsNewer(string current, string latest)
    {
        var currentVer = Parse(current);
        var latestVer = Parse(latest);
        return latestVer > currentVer;
    }

    public static Version Parse(string versionString)
    {
        var cleaned = versionString.TrimStart('v', 'V').Trim();
        var dashIndex = cleaned.IndexOf('-');
        if (dashIndex > 0) cleaned = cleaned[..dashIndex];

        return Version.TryParse(cleaned, out var version) ? version : new Version(0, 0, 0);
    }
}
