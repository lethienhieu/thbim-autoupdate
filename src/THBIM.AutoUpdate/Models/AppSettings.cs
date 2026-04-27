using System.Text.Json.Serialization;

namespace THBIM.AutoUpdate.Models;

public class AppSettings
{
    [JsonPropertyName("checkIntervalMinutes")]
    public int CheckIntervalMinutes { get; set; } = 1440;

    [JsonPropertyName("autoUpdate")]
    public bool AutoUpdate { get; set; } = true;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = true;

    [JsonPropertyName("notifications")]
    public bool Notifications { get; set; } = true;

    [JsonPropertyName("selfUpdateOwner")]
    public string SelfUpdateOwner { get; set; } = "lethienhieu";

    [JsonPropertyName("selfUpdateRepo")]
    public string SelfUpdateRepo { get; set; } = "thbim-autoupdate";

    [JsonPropertyName("addins")]
    public List<AddinConfig> Addins { get; set; } = [];

    /// <summary>
    /// Per-tool installed versions. Key = tool name, Value = version string.
    /// </summary>
    [JsonPropertyName("toolVersions")]
    public Dictionary<string, string> ToolVersions { get; set; } = new();

    public string GetToolVersion(string toolName) =>
        ToolVersions.TryGetValue(toolName, out var v) ? v : "0.0.0";

    public void SetToolVersion(string toolName, string version) =>
        ToolVersions[toolName] = version;
}
