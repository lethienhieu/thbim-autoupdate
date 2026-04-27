using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using THBIM.AutoUpdate.Helpers;

namespace THBIM.AutoUpdate.Views;

public enum NotificationType
{
    UpdateAvailable,      // AutoUpdate OFF → "New version available, download?"
    AutoUpdateReady,      // Downloading/installing progress
    UpdatePartial,        // Some files installed, others waiting for Revit to close
    UpdateSuccess,        // Update completed successfully
    UpdateFailed,         // Update failed
    SelfUpdate            // App updating itself — forced, no cancel
}

public partial class NotificationPopup : Window
{
    public bool UserClickedUpdate { get; private set; }
    public bool UserClickedDismiss { get; private set; }

    public NotificationPopup()
    {
        InitializeComponent();
    }

    public static NotificationPopup Create(NotificationType type, string version, int toolCount = 0, string? error = null, string? changelog = null)
    {
        var popup = new NotificationPopup();

        switch (type)
        {
            case NotificationType.UpdateAvailable:
                popup.SubtitleText.Text = $"v{version} available";
                popup.MessageText.Text = "A new update is available.\nWould you like to download and install now?";
                popup.SetChangelog(changelog);
                popup.AddButton("Download & Install", "#FFD700", "#1a1a1a", true);
                popup.AddButton("Later", "#333333", "#AAAAAA", false);
                break;

            case NotificationType.AutoUpdateReady:
                popup.SubtitleText.Text = $"Updating to v{version}...";
                popup.MessageText.Text = "Downloading update...\nThis may take a moment.";
                popup.SetChangelog(changelog);
                popup.ProgressPanel.Visibility = Visibility.Visible;
                popup.AddButton("Cancel", "#333333", "#AAAAAA", false);
                break;

            case NotificationType.UpdatePartial:
                popup.SubtitleText.Text = "Update Partially Installed";
                popup.MessageText.Text = $"Updated to v{version}.\n{toolCount} file(s) will install automatically when Revit closes.";
                popup.SetChangelog(changelog);
                popup.AddButton("OK, got it", "#FFD700", "#1a1a1a", true);
                break;

            case NotificationType.UpdateSuccess:
                popup.SubtitleText.Text = "Update Complete ✓";
                popup.MessageText.Text = ProcessHelper.IsRevitRunning()
                    ? $"Successfully updated to v{version}.\nPress the Reload button in Revit to apply changes."
                    : $"Successfully updated to v{version}.\nChanges will apply next time you open Revit.";
                popup.SetChangelog(changelog);
                popup.AddButton("Great!", "#FFD700", "#1a1a1a", true);
                break;

            case NotificationType.UpdateFailed:
                popup.SubtitleText.Text = "Update Failed";
                popup.MessageText.Text = error ?? "An error occurred during the update.";
                popup.MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66));
                popup.AddButton("Try Again", "#FFD700", "#1a1a1a", true);
                popup.AddButton("Dismiss", "#333333", "#AAAAAA", false);
                break;

            case NotificationType.SelfUpdate:
                popup.SubtitleText.Text = $"App update v{version}";
                popup.MessageText.Text = "A new version of THBIM AutoUpdate is available.\nThe app will update and restart automatically.";
                popup.ProgressPanel.Visibility = Visibility.Visible;
                popup.CloseBtn.Visibility = Visibility.Collapsed;
                // No buttons — forced update, cannot cancel
                break;
        }

        return popup;
    }

    private void AddButton(string text, string bgHex, string fgHex, bool isPrimary)
    {
        var btn = new Button
        {
            Content = text,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex)),
            Height = 38,
            MinWidth = 130,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(6, 0, 6, 0),
            Style = (Style)FindResource("ActionButton")
        };

        btn.Click += (_, _) =>
        {
            UserClickedUpdate = isPrimary;
            UserClickedDismiss = !isPrimary;
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this) == false
                && System.Windows.Interop.ComponentDispatcher.IsThreadModal)
                DialogResult = isPrimary;
            else
                Close();
        };

        ButtonPanel.Children.Add(btn);
    }

    private string? _changelog;

    private void SetChangelog(string? changelog)
    {
        _changelog = changelog;
        if (!string.IsNullOrWhiteSpace(changelog))
        {
            ChangelogText.Text = changelog;
            ChangelogText.Visibility = Visibility.Visible;
        }
    }

    public void UpdateProgress(double percent, string text)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = percent;
            ProgressText.Text = text;
        });
    }

    public void ShowSuccess(string version, int toolCount, string? changelog = null, bool revitRunning = false)
    {
        Dispatcher.Invoke(() =>
        {
            SubtitleText.Text = "Update Complete ✓";
            MessageText.Text = revitRunning
                ? $"Successfully updated to v{version}.\nPress the Reload button in Revit to apply changes."
                : $"Successfully updated to v{version}.\nChanges will apply next time you open Revit.";
            MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            ProgressPanel.Visibility = Visibility.Collapsed;
            SetChangelog(changelog ?? _changelog);
            ButtonPanel.Children.Clear();
            AddButton("Great!", "#FFD700", "#1a1a1a", true);
        });
    }

    public void ShowError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            SubtitleText.Text = "Update Failed";
            MessageText.Text = error;
            MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66));
            ProgressPanel.Visibility = Visibility.Collapsed;
            ButtonPanel.Children.Clear();
            AddButton("Try Again", "#FFD700", "#1a1a1a", true);
            AddButton("Dismiss", "#333333", "#AAAAAA", false);
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        UserClickedDismiss = true;
        if (System.Windows.Interop.ComponentDispatcher.IsThreadModal)
            DialogResult = false;
        else
            Close();
    }
}
