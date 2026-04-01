using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using THBIM.AutoUpdate.Services;
using THBIM.AutoUpdate.ViewModels;
using THBIM.AutoUpdate.Views;

namespace THBIM.AutoUpdate;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Relocate exe to canonical path if running from elsewhere (Downloads, Desktop, etc.)
        var canonicalExe = StartupService.CanonicalExePath;
        var currentExe = Environment.ProcessPath!;
        if (!string.Equals(currentExe, canonicalExe, StringComparison.OrdinalIgnoreCase))
        {
            var canonicalDir = Path.GetDirectoryName(canonicalExe)!;
            Directory.CreateDirectory(canonicalDir);
            File.Copy(currentExe, canonicalExe, true);
            Process.Start(new ProcessStartInfo
            {
                FileName = canonicalExe,
                Arguments = string.Join(" ", e.Args),
                UseShellExecute = true
            });
            Shutdown();
            return;
        }

        // Create services
        var settingsService = new SettingsService();
        var historyService = new HistoryService();
        var githubService = new GitHubReleaseService();
        var updateService = new UpdateService();
        var notificationService = new NotificationService();
        var startupService = new StartupService();
        var licenseService = new LicenseService();

        // Auto-register startup if setting is enabled but registry not set or path is wrong
        var settings = settingsService.Load();
        if (settings.StartWithWindows && !startupService.IsPathCorrect())
            startupService.SetEnabled(true);

        // Create ViewModel
        _mainViewModel = new MainViewModel(
            githubService, updateService, settingsService,
            historyService, notificationService, startupService, licenseService);

        // Create MainWindow
        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        // Create Tray Icon with THBIM logo
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/thbim-icon.ico"))?.Stream;
        var trayIcon = iconStream != null ? new System.Drawing.Icon(iconStream) : System.Drawing.SystemIcons.Application;
        _trayIcon = new TaskbarIcon
        {
            Icon = trayIcon,
            ToolTipText = "THBIM AutoUpdate",
            ContextMenu = CreateTrayMenu(),
            MenuActivation = PopupActivationMode.RightClick
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // Wire tray icon to notification service for balloon tips
        notificationService.SetTrayIcon(_trayIcon);

        // Show on first launch, otherwise start minimized
        if (!e.Args.Contains("--minimized"))
        {
            ShowMainWindow();
        }
    }

    private ContextMenu CreateTrayMenu()
    {
        var menu = new ContextMenu
        {
            Background = System.Windows.Media.Brushes.Black,
            Foreground = System.Windows.Media.Brushes.White,
        };

        var openItem = new MenuItem { Header = "Open THBIM Update" };
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var checkItem = new MenuItem { Header = "Check for Updates" };
        checkItem.Click += async (_, _) =>
        {
            if (_mainViewModel != null)
                await _mainViewModel.CheckForUpdatesAsync();
        };
        menu.Items.Add(checkItem);

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) =>
        {
            ShowMainWindow();
            if (_mainViewModel != null) _mainViewModel.IsSettingsOpen = true;
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var aboutItem = new MenuItem { Header = $"About THBIM AutoUpdate v{MainViewModel.AppVersion}" };
        menu.Items.Add(aboutItem);

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) =>
        {
            _mainViewModel?.Dispose();
            _trayIcon?.Dispose();
            Shutdown();
        };
        menu.Items.Add(quitItem);

        return menu;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
