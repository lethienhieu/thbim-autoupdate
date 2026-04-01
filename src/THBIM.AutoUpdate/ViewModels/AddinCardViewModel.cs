using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.ViewModels;

public class AddinCardViewModel : ViewModelBase
{
    public AddinCardViewModel(ToolInfo tool, string installedVersion)
    {
        ToolName = tool.Name;
        _description = tool.Description;
        _iconType = tool.IconType;
        _installedVersion = installedVersion;
        _latestVersion = tool.Version;
        _changelog = tool.Changelog;
    }

    /// <summary>
    /// Constructor for fallback when no manifest (single-package mode).
    /// </summary>
    public AddinCardViewModel(AddinConfig config)
    {
        ToolName = config.Name;
        _description = config.Description;
        _iconType = config.IconType;
        _installedVersion = config.InstalledVersion;
    }

    public string ToolName { get; }

    private string _description = "";
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _iconType = "revit";
    public string IconType
    {
        get => _iconType;
        set => SetProperty(ref _iconType, value);
    }

    private string _installedVersion = "0.0.0";
    public string InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (SetProperty(ref _installedVersion, value))
                OnPropertyChanged(nameof(VersionDisplay));
        }
    }

    private string _latestVersion = "";
    public string LatestVersion
    {
        get => _latestVersion;
        set
        {
            if (SetProperty(ref _latestVersion, value))
                OnPropertyChanged(nameof(VersionDisplay));
        }
    }

    private string _changelog = "";
    public string Changelog
    {
        get => _changelog;
        set => SetProperty(ref _changelog, value);
    }

    private AddinStatus _status = AddinStatus.UpToDate;
    public AddinStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(VersionDisplay));
                OnPropertyChanged(nameof(IsUpdating));
                OnPropertyChanged(nameof(ShowProgress));
                OnPropertyChanged(nameof(ShowChangelog));
            }
        }
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (SetProperty(ref _downloadProgress, value))
                OnPropertyChanged(nameof(ProgressText));
        }
    }

    private long _downloadedBytes;
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set
        {
            if (SetProperty(ref _downloadedBytes, value))
                OnPropertyChanged(nameof(ProgressText));
        }
    }

    private long _totalBytes;
    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (SetProperty(ref _totalBytes, value))
                OnPropertyChanged(nameof(ProgressText));
        }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    // Display name shown in UI
    public string Name => ToolName;

    public string StatusText => Status switch
    {
        AddinStatus.UpToDate => "LATEST",
        AddinStatus.UpdateAvailable => "UPDATE",
        AddinStatus.Updating => "UPDATING",
        AddinStatus.Error => "ERROR",
        _ => ""
    };

    public string VersionDisplay => Status switch
    {
        AddinStatus.UpdateAvailable => $"v{InstalledVersion} → v{LatestVersion}",
        AddinStatus.Updating => $"Updating to v{LatestVersion}...",
        _ => $"v{InstalledVersion}"
    };

    public string ProgressText
    {
        get
        {
            if (TotalBytes <= 0) return $"Downloading... {DownloadProgress:P0}";
            return $"Downloading... {DownloadProgress:P0}    {FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
        }
    }

    public bool IsUpdating => Status == AddinStatus.Updating;
    public bool ShowProgress => Status == AddinStatus.Updating;
    public bool ShowChangelog => Status == AddinStatus.UpdateAvailable && !string.IsNullOrEmpty(Changelog);

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };
}
