using System.Text.Json.Serialization;

namespace THBIM.AutoUpdate.Models;

public class ToolManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "";

    [JsonPropertyName("changelog")]
    public List<string> Changelog { get; set; } = [];

    [JsonPropertyName("appAsset")]
    public string AppAsset { get; set; } = "THBIM.AutoUpdate.exe";

    [JsonPropertyName("tools")]
    public List<ToolInfo> Tools { get; set; } = [];
}

public class ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = "";

    [JsonPropertyName("iconType")]
    public string IconType { get; set; } = "revit";

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];
}
