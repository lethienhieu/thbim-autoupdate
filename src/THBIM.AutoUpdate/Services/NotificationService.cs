using Hardcodet.Wpf.TaskbarNotification;

namespace THBIM.AutoUpdate.Services;

public class NotificationService
{
    private TaskbarIcon? _trayIcon;

    public void SetTrayIcon(TaskbarIcon trayIcon)
    {
        _trayIcon = trayIcon;
    }

    public void ShowUpdateAvailable(string addinName, string newVersion)
    {
        ShowBalloon("Update Available", $"{addinName} {newVersion} is ready to install", BalloonIcon.Info);
    }

    public void ShowUpdateComplete(string addinName, string version)
    {
        ShowBalloon("Update Complete", $"{addinName} {version} installed successfully", BalloonIcon.Info);
    }

    public void ShowUpdateFailed(string addinName, string error)
    {
        ShowBalloon("Update Failed", $"{addinName}: {error}", BalloonIcon.Error);
    }

    private void ShowBalloon(string title, string message, BalloonIcon icon)
    {
        try
        {
            _trayIcon?.ShowBalloonTip(title, message, icon);
        }
        catch { }
    }
}
