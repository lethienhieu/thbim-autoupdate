using System.IO;
using System.Text.Json;
using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.Services;

public class HistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "THBIM", "AutoUpdate", "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private List<UpdateHistoryEntry> _entries = [];

    public List<UpdateHistoryEntry> Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                _entries = JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(json, JsonOptions) ?? [];
            }
        }
        catch { _entries = []; }
        return _entries;
    }

    public void AddEntry(UpdateHistoryEntry entry)
    {
        _entries.Add(entry);
        Save();
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(HistoryPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(HistoryPath, json);
    }
}
