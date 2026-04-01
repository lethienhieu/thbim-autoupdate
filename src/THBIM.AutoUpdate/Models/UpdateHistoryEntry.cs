using System.Text.Json.Serialization;

namespace THBIM.AutoUpdate.Models;

public class UpdateHistoryEntry
{
    [JsonPropertyName("addinName")]
    public string AddinName { get; set; } = "";

    [JsonPropertyName("fromVersion")]
    public string FromVersion { get; set; } = "";

    [JsonPropertyName("toVersion")]
    public string ToVersion { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
