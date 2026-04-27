using System.Net.Http;
using System.Text.Json;
using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.Services;

public class GitHubReleaseService
{
    private static readonly HttpClient Http = new();

    static GitHubReleaseService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("THBIM-AutoUpdate/1.0");
        Http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            var response = await Http.GetAsync(url);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<GitHubRelease>> GetRecentReleasesAsync(string owner, string repo, int count = 10)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page={count}";
            var response = await Http.GetAsync(url);

            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<ToolManifest?> GetManifestAsync(GitHubRelease release)
    {
        try
        {
            var manifestAsset = release.Assets.FirstOrDefault(a =>
                a.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

            if (manifestAsset == null) return null;

            var response = await Http.GetAsync(manifestAsset.BrowserDownloadUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ToolManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    public string? FindAssetUrl(GitHubRelease release, string assetPattern, int? year = null)
    {
        var pattern = year.HasValue
            ? assetPattern.Replace("{year}", year.Value.ToString())
            : assetPattern;

        var asset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase));

        // Fallback: partial match
        asset ??= release.Assets.FirstOrDefault(a =>
            a.Name.Contains(pattern.Replace(".zip", ""), StringComparison.OrdinalIgnoreCase)
            && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        // Fallback: first ZIP
        asset ??= release.Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return asset?.BrowserDownloadUrl;
    }
}
