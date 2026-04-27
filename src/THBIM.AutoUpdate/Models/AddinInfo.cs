using System.Text.Json.Serialization;

namespace THBIM.AutoUpdate.Models;

public class AddinConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    [JsonPropertyName("revitYears")]
    public List<int> RevitYears { get; set; } = [2024, 2025, 2026];

    [JsonPropertyName("assetPattern")]
    public string AssetPattern { get; set; } = "";

    [JsonPropertyName("iconType")]
    public string IconType { get; set; } = "revit";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("installedVersion")]
    public string InstalledVersion { get; set; } = "0.0.0";
}
