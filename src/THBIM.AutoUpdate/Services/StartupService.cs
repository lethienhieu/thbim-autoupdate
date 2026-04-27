using System.IO;
using Microsoft.Win32;

namespace THBIM.AutoUpdate.Services;

public class StartupService
{
    private const string AppName = "THBIM AutoUpdate";
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Canonical exe path: %AppData%\THBIM\AutoUpdate\THBIM.AutoUpdate.exe
    /// </summary>
    public static string CanonicalExePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "THBIM", "AutoUpdate", "THBIM.AutoUpdate.exe");

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    /// <summary>
    /// Check if registry path matches canonical path. Returns false if key missing or path wrong.
    /// </summary>
    public bool IsPathCorrect()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        var val = key?.GetValue(AppName) as string;
        if (val == null) return false;
        var expected = $"\"{CanonicalExePath}\" --minimized";
        return string.Equals(val, expected, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key == null) return;

        if (enabled)
        {
            key.SetValue(AppName, $"\"{CanonicalExePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
