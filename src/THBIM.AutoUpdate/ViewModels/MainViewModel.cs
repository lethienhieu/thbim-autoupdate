using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using THBIM.AutoUpdate.Helpers;
using THBIM.AutoUpdate.Models;
using THBIM.AutoUpdate.Services;
using THBIM.AutoUpdate.Views;

namespace THBIM.AutoUpdate.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly GitHubReleaseService _githubService;
    private readonly UpdateService _updateService;
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService;
    private readonly NotificationService _notificationService;
    private readonly StartupService _startupService;
    private readonly SelfUpdateService _selfUpdateService;
    private readonly AppSettings _settings;
    private Timer? _checkTimer;
    public const string AppVersion = "1.0.9";
    private const string CurrentAppVersion = AppVersion;
    public string AppVersionText => $"v{CurrentAppVersion}";

    // Cached release info for downloading
    private GitHubRelease? _latestRelease;
    private string? _zipDownloadUrl;
    private string? _changelogText;

    // Pending files waiting for Revit to close
    private PendingContext? _pendingContext;
    private CancellationTokenSource? _pendingWatcherCts;

    public ObservableCollection<AddinCardViewModel> Addins { get; } = [];

    public MainViewModel(
        GitHubReleaseService githubService,
        UpdateService updateService,
        SettingsService settingsService,
        HistoryService historyService,
        NotificationService notificationService,
        StartupService startupService,
        LicenseService licenseService)
    {
        _githubService = githubService;
        _updateService = updateService;
        _settingsService = settingsService;
        _historyService = historyService;
        _notificationService = notificationService;
        _startupService = startupService;
        _selfUpdateService = new SelfUpdateService();

        _settings = settingsService.Load();
        _historyService.Load();

        SettingsVM = new SettingsViewModel(_settings, settingsService, startupService);
        SettingsVM.IntervalChanged += OnIntervalChanged;

        AccountVM = new AccountViewModel(licenseService);
        AccountVM.LoggedInChanged += OnLoggedInChanged;

        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(), () => !IsChecking);
        UpdateAllCommand = new RelayCommand(async () => await UpdateAllAsync(), () => !IsUpdatingAll && UpdatesAvailableCount > 0 && AccountVM.IsLoggedIn);
        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        UninstallCommand = new RelayCommand(async () => await UninstallAsync());
        SwitchTabCommand = new RelayCommand<int>(tab => SelectedTab = tab);

        StartTimer();
    }

    public SettingsViewModel SettingsVM { get; }
    public AccountViewModel AccountVM { get; }

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public ICommand SwitchTabCommand { get; }

    private void OnLoggedInChanged(bool isLoggedIn)
    {
        OnPropertyChanged(nameof(UpdateButtonText));
    }

    private string _statusText = "Connecting to server...";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _versionText = "—";
    public string VersionText
    {
        get => _versionText;
        set => SetProperty(ref _versionText, value);
    }

    private string _lastCheckedText = "Never checked";
    public string LastCheckedText
    {
        get => _lastCheckedText;
        set => SetProperty(ref _lastCheckedText, value);
    }

    private int _updatesAvailableCount;
    public int UpdatesAvailableCount
    {
        get => _updatesAvailableCount;
        set
        {
            if (SetProperty(ref _updatesAvailableCount, value))
                OnPropertyChanged(nameof(UpdateButtonText));
        }
    }

    private bool _isChecking;
    public bool IsChecking
    {
        get => _isChecking;
        set => SetProperty(ref _isChecking, value);
    }

    private bool _isUpdatingAll;
    public bool IsUpdatingAll
    {
        get => _isUpdatingAll;
        set => SetProperty(ref _isUpdatingAll, value);
    }

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string UpdateButtonText => !AccountVM.IsLoggedIn
        ? "🔒 Please login to update"
        : UpdatesAvailableCount > 0
            ? $"↵ Update All ({UpdatesAvailableCount} available)"
            : "✓ All addins are up to date";

    public int AddinCount => Addins.Count;

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand UninstallCommand { get; }

    private void StartTimer()
    {
        var interval = TimeSpan.FromMinutes(_settings.CheckIntervalMinutes);
        _checkTimer = new Timer(_ => RunOnUI(async () => await CheckForUpdatesAsync()),
            null, TimeSpan.FromSeconds(5), interval);
    }

    private void OnIntervalChanged(int minutes)
    {
        _checkTimer?.Change(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes));
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(action);
    }

    public async Task CheckForUpdatesAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        StatusText = "Checking for updates...";

        try
        {
            // 0. Check if app itself needs update (from thbim-autoupdate repo)
            if (await CheckSelfUpdateAsync())
                return; // App will restart

            var addinConfig = _settings.Addins.FirstOrDefault(a => a.Enabled);
            if (addinConfig == null)
            {
                StatusText = "No addins configured";
                return;
            }

            // 1. Get latest release from GitHub
            _latestRelease = await _githubService.GetLatestReleaseAsync(addinConfig.Owner, addinConfig.Repo);
            if (_latestRelease == null)
            {
                IsConnected = false;
                StatusText = "Failed to reach server";
                return;
            }

            IsConnected = true;
            var releaseVersion = _latestRelease.TagName.TrimStart('v', 'V');
            VersionText = $"v{releaseVersion}";
            _zipDownloadUrl = _githubService.FindAssetUrl(_latestRelease, addinConfig.AssetPattern);

            // 2. Try to get manifest.json from release assets
            var manifest = await _githubService.GetManifestAsync(_latestRelease);

            // 3. Check if addins are actually installed on disk
            var addinsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", "2025", "TH Tools");
            var isFirstInstall = !Directory.Exists(addinsDir) || Directory.GetFiles(addinsDir, "*.dll").Length == 0;

            // 4. Single version comparison — one update card with changelog
            Addins.Clear();
            var updatesCount = 0;
            var manifestVersion = manifest?.Version ?? releaseVersion;

            if (isFirstInstall || VersionComparer.IsNewer(addinConfig.InstalledVersion, manifestVersion))
            {
                // Build changelog text from manifest
                _changelogText = "";
                if (manifest?.Changelog is { Count: > 0 })
                    _changelogText = string.Join("\n", manifest.Changelog.Select(c => $"• {c}"));
                var changelogText = _changelogText;

                var card = new AddinCardViewModel(addinConfig)
                {
                    LatestVersion = manifestVersion,
                    Status = AddinStatus.UpdateAvailable,
                    Changelog = changelogText
                };
                Addins.Add(card);
                updatesCount = 1;
            }
            else
            {
                var card = new AddinCardViewModel(addinConfig)
                {
                    LatestVersion = manifestVersion,
                    Status = AddinStatus.UpToDate
                };
                Addins.Add(card);
            }

            UpdatesAvailableCount = updatesCount;
            OnPropertyChanged(nameof(AddinCount));
            StatusText = updatesCount > 0
                ? $"Connected — Update v{manifestVersion} available"
                : "Connected — All tools up to date";

            if (updatesCount > 0)
                await HandleUpdateNotificationAsync(updatesCount, manifestVersion);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Connection failed — {ex.Message}";
        }
        finally
        {
            IsChecking = false;
            LastCheckedText = "Last checked: just now";
            OnPropertyChanged(nameof(UpdateButtonText));
        }
    }

    private async Task HandleUpdateNotificationAsync(int updatesCount, string version)
    {
        if (_settings.AutoUpdate)
        {
            // AutoUpdate ON → install directly (no Revit check needed)
            await PerformInstallAsync(updatesCount, version);
        }
        else
        {
            // AutoUpdate OFF → ask user
            var shouldUpdate = false;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var popup = NotificationPopup.Create(
                    NotificationType.UpdateAvailable, version, updatesCount, changelog: _changelogText);
                popup.ShowDialog();
                shouldUpdate = popup.UserClickedUpdate;
            });

            if (shouldUpdate)
                await PerformInstallAsync(updatesCount, version);
        }
    }

    private async Task PerformInstallAsync(int updatesCount, string version)
    {
        NotificationPopup? progressPopup = null;
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            progressPopup = NotificationPopup.Create(
                NotificationType.AutoUpdateReady, version, updatesCount, changelog: _changelogText);
            progressPopup.Show();
        });

        await UpdateAllAsync(progressPopup);
    }

    public async Task UpdateAllAsync(NotificationPopup? progressPopup = null)
    {
        if (IsUpdatingAll || string.IsNullOrEmpty(_zipDownloadUrl)) return;
        IsUpdatingAll = true;

        var addinConfig = _settings.Addins.FirstOrDefault(a => a.Enabled);
        if (addinConfig == null) return;

        // Mark all updatable tools as Updating
        var updatableTools = Addins.Where(a => a.Status == AddinStatus.UpdateAvailable).ToList();
        foreach (var tool in updatableTools)
            tool.Status = AddinStatus.Updating;

        // Download the single ZIP (contains all tools)
        var firstTool = updatableTools.FirstOrDefault();
        var progress = new Progress<(long downloaded, long total)>(p =>
        {
            // Show progress on first tool card
            if (firstTool != null)
            {
                firstTool.DownloadedBytes = p.downloaded;
                firstTool.TotalBytes = p.total;
                firstTool.DownloadProgress = p.total > 0 ? (double)p.downloaded / p.total : 0;
            }
            // Update popup progress
            if (progressPopup != null && p.total > 0)
            {
                var pct = (double)p.downloaded / p.total * 100;
                var dlMB = p.downloaded / 1024.0 / 1024.0;
                var totalMB = p.total / 1024.0 / 1024.0;
                progressPopup.UpdateProgress(pct, $"{dlMB:F1} / {totalMB:F1} MB ({pct:F0}%)");
            }
        });

        try
        {
            var newVersion = _latestRelease?.TagName.TrimStart('v', 'V') ?? "unknown";
            var result = await _updateService.DownloadAndInstallAsync(
                addinConfig, _zipDownloadUrl, newVersion, progress);

            if (result.Success)
            {
                // All files copied successfully
                foreach (var tool in updatableTools)
                {
                    tool.InstalledVersion = tool.LatestVersion;
                    tool.Status = AddinStatus.UpToDate;
                }

                addinConfig.InstalledVersion = newVersion;

                _historyService.AddEntry(new UpdateHistoryEntry
                {
                    AddinName = "THBIM Revit Tools",
                    FromVersion = addinConfig.InstalledVersion,
                    ToVersion = newVersion,
                    Timestamp = DateTime.Now,
                    Success = true
                });

                var revitRunning = ProcessHelper.IsRevitRunning();

                // Show success popup
                if (progressPopup != null)
                    progressPopup.ShowSuccess(newVersion, updatableTools.Count, _changelogText, revitRunning);
                else if (_settings.Notifications)
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var popup = NotificationPopup.Create(
                            NotificationType.UpdateSuccess, newVersion, updatableTools.Count, changelog: _changelogText);
                        popup.ShowDialog();
                    });
            }
            else if (result.HasPending && result.Pending != null)
            {
                // Partial success — some files installed, others locked by Revit
                foreach (var tool in updatableTools)
                {
                    tool.InstalledVersion = tool.LatestVersion;
                    tool.Status = AddinStatus.UpToDate;
                }

                addinConfig.InstalledVersion = newVersion;

                _historyService.AddEntry(new UpdateHistoryEntry
                {
                    AddinName = "THBIM Revit Tools",
                    FromVersion = addinConfig.InstalledVersion,
                    ToVersion = newVersion,
                    Timestamp = DateTime.Now,
                    Success = true
                });

                var pendingCount = result.Pending.Files.Count;

                // Show partial popup
                if (progressPopup != null)
                {
                    progressPopup.Dispatcher.Invoke(() =>
                    {
                        progressPopup.SubtitleText.Text = "Update Partially Installed";
                        progressPopup.MessageText.Text = $"Updated to v{newVersion}.\n{pendingCount} file(s) will install automatically when Revit closes.";
                        progressPopup.ProgressPanel.Visibility = Visibility.Collapsed;
                        progressPopup.ButtonPanel.Children.Clear();
                    });
                }
                else if (_settings.Notifications)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var popup = NotificationPopup.Create(
                            NotificationType.UpdatePartial, newVersion, pendingCount, changelog: _changelogText);
                        popup.ShowDialog();
                    });
                }

                // Start background watcher for Revit close to install pending files
                StatusText = $"Waiting for Revit to close ({pendingCount} file(s) pending)...";
                _pendingContext = result.Pending;
                _pendingWatcherCts?.Cancel();
                _pendingWatcherCts = new CancellationTokenSource();
                _ = WatchForRevitCloseAndInstallPendingAsync(newVersion, _pendingWatcherCts.Token);
            }
            else
            {
                // Hard failure
                foreach (var tool in updatableTools)
                {
                    tool.Status = AddinStatus.Error;
                    tool.ErrorMessage = result.Error;
                }

                if (progressPopup != null)
                    progressPopup.ShowError(result.Error ?? "Unknown error");
                else if (_settings.Notifications)
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var popup = NotificationPopup.Create(
                            NotificationType.UpdateFailed, "", 0, result.Error);
                        popup.ShowDialog();
                    });
            }
        }
        catch (Exception ex)
        {
            foreach (var tool in updatableTools)
            {
                tool.Status = AddinStatus.Error;
                tool.ErrorMessage = ex.Message;
            }
        }

        IsUpdatingAll = false;
        UpdatesAvailableCount = Addins.Count(a => a.Status == AddinStatus.UpdateAvailable);
        _settingsService.Save(_settings);
        OnPropertyChanged(nameof(UpdateButtonText));
    }

    /// <summary>
    /// Watch for Revit to close, then install pending files.
    /// </summary>
    private async Task WatchForRevitCloseAndInstallPendingAsync(string version, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(5000, ct);

                if (!ProcessHelper.IsRevitRunning())
                {
                    // Revit closed — wait a moment for cleanup
                    await Task.Delay(3000, ct);

                    if (_pendingContext == null) return;

                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        StatusText = "Installing remaining files...";
                        var pendingResult = await _updateService.InstallPendingAsync(_pendingContext);
                        _pendingContext = null;

                        if (pendingResult.Success)
                        {
                            StatusText = $"Connected — All tools up to date (v{version})";
                            if (_settings.Notifications)
                            {
                                var popup = NotificationPopup.Create(
                                    NotificationType.UpdateSuccess, version, 0, changelog: _changelogText);
                                popup.ShowDialog();
                            }
                        }
                        else
                        {
                            StatusText = $"Some files could not be installed: {pendingResult.Error}";
                        }
                    });
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<bool> CheckSelfUpdateAsync()
    {
        try
        {
            var release = await _githubService.GetLatestReleaseAsync(
                _settings.SelfUpdateOwner, _settings.SelfUpdateRepo);
            if (release == null) return false;

            var latestVersion = release.TagName.TrimStart('v', 'V');
            if (!VersionComparer.IsNewer(CurrentAppVersion, latestVersion))
                return false;

            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (asset == null) return false;

            await SelfUpdateAsync(latestVersion, asset.BrowserDownloadUrl);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SelfUpdateAsync(string newVersion, string downloadUrl)
    {

        // Show popup — user cannot cancel, app will restart
        NotificationPopup? popup = null;
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            popup = NotificationPopup.Create(
                NotificationType.SelfUpdate, newVersion, 0);
            popup.Show();
        });

        var progress = new Progress<(long downloaded, long total)>(p =>
        {
            if (popup != null && p.total > 0)
            {
                var pct = (double)p.downloaded / p.total * 100;
                var dlMB = p.downloaded / 1024.0 / 1024.0;
                var totalMB = p.total / 1024.0 / 1024.0;
                popup.UpdateProgress(pct, $"{dlMB:F1} / {totalMB:F1} MB ({pct:F0}%)");
            }
        });

        try
        {
            var shouldExit = await _selfUpdateService.UpdateSelfAsync(downloadUrl, progress);

            if (shouldExit)
            {
                // Save settings before exit
                _settingsService.Save(_settings);
                _checkTimer?.Dispose();

                // Exit app — bat script will replace exe and restart
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
        }
        catch (Exception ex)
        {
            popup?.ShowError($"Self-update failed: {ex.Message}");
        }
    }

    private async Task UninstallAsync()
    {
        var result = MessageBox.Show(
            "This will remove all THBIM addins from Revit and uninstall the AutoUpdate app.\n\nAre you sure?",
            "Uninstall THBIM", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var uninstallService = new UninstallService();

        // Step 1: Remove addins
        StatusText = "Removing THBIM addins...";
        var (removed, errors) = uninstallService.RemoveAddins();

        if (errors.Count > 0)
        {
            var msg = $"Removed {removed} Revit version(s), but some errors occurred:\n{string.Join("\n", errors)}\n\nContinue uninstalling the app?";
            if (MessageBox.Show(msg, "Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                StatusText = "Uninstall cancelled";
                return;
            }
        }

        // Step 2: Remove app
        StatusText = "Uninstalling app...";
        Dispose();
        uninstallService.RemoveApp();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _pendingWatcherCts?.Cancel();
        _checkTimer?.Dispose();
    }
}
