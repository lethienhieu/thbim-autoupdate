using THBIM.AutoUpdate.Models;
using THBIM.AutoUpdate.Services;

namespace THBIM.AutoUpdate.ViewModels;

public class IntervalOption
{
    public string Label { get; init; } = "";
    public int Minutes { get; init; }
}

public class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;

    public static List<IntervalOption> IntervalOptions { get; } =
    [
        new() { Label = "30 minutes", Minutes = 30 },
        new() { Label = "1 Hour", Minutes = 60 },
        new() { Label = "6 Hours", Minutes = 360 },
        new() { Label = "1 Day", Minutes = 1440 },
        new() { Label = "1 Week", Minutes = 10080 }
    ];

    public SettingsViewModel(AppSettings settings, SettingsService settingsService, StartupService startupService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _startupService = startupService;
        _selectedInterval = IntervalOptions.FirstOrDefault(o => o.Minutes == settings.CheckIntervalMinutes)
                            ?? IntervalOptions[3];
    }

    public bool AutoUpdate
    {
        get => _settings.AutoUpdate;
        set
        {
            if (_settings.AutoUpdate == value) return;
            _settings.AutoUpdate = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows == value) return;
            _settings.StartWithWindows = value;
            _startupService.SetEnabled(value);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool Notifications
    {
        get => _settings.Notifications;
        set
        {
            if (_settings.Notifications == value) return;
            _settings.Notifications = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    private IntervalOption _selectedInterval;
    public IntervalOption SelectedInterval
    {
        get => _selectedInterval;
        set
        {
            if (SetProperty(ref _selectedInterval, value) && value != null)
            {
                _settings.CheckIntervalMinutes = value.Minutes;
                SaveSettings();
                IntervalChanged?.Invoke(value.Minutes);
            }
        }
    }

    public string RepoInfo
    {
        get
        {
            var first = _settings.Addins.FirstOrDefault();
            return first != null ? "THBIM Cloud" : "Not configured";
        }
    }

    public event Action<int>? IntervalChanged;

    private void SaveSettings() => _settingsService.Save(_settings);
}
