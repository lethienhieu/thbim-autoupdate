namespace THBIM.AutoUpdate.Models;

public class ReleaseHistoryItem
{
    public string Version { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public List<string> ChangelogLines { get; set; } = [];
    public bool IsCurrent { get; set; }

    public string VersionDisplay => $"v{Version}";
    public string DateDisplay => PublishedAt == default
        ? ""
        : PublishedAt.ToLocalTime().ToString("MMM d, yyyy");
}
