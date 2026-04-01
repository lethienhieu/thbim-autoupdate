using System.IO;
using System.Text.Json;
using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.Services;

public class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "THBIM", "AutoUpdate");

    private static readonly string SettingsPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefault();
            }
        }
        catch { }
        return CreateDefault();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private AppSettings CreateDefault()
    {
        var settings = new AppSettings
        {
            CheckIntervalMinutes = 1440,
            AutoUpdate = true,
            StartWithWindows = true,
            Notifications = true,
            Addins = DetectDefaultAddins()
        };
        Save(settings);
        return settings;
    }

    private List<AddinConfig> DetectDefaultAddins()
    {
        return
        [
            new AddinConfig
            {
                Name = "THBIM Revit Tools",
                Description = "Core structural modeling tools",
                Owner = "lethienhieu",
                Repo = "revit-tool",
                RevitYears = DetectRevitYears(),
                AssetPattern = "THBIM-RevitTools.zip",
                IconType = "revit"
            }
        ];
    }

    private List<int> DetectRevitYears()
    {
        var years = new List<int>();
        var addinsBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins");

        for (int year = 2022; year <= 2027; year++)
        {
            if (Directory.Exists(Path.Combine(addinsBase, year.ToString())))
                years.Add(year);
        }
        return years.Count > 0 ? years : [2025];
    }
}
