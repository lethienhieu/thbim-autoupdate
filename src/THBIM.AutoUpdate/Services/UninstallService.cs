using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace THBIM.AutoUpdate.Services;

public class UninstallService
{
    private static readonly string[] RevitYears = ["2021", "2022", "2023", "2024", "2025", "2026"];

    /// <summary>
    /// Remove all THBIM addins from AppData for all Revit versions.
    /// </summary>
    public (int removedVersions, List<string> errors) RemoveAddins()
    {
        var appDataAddins = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins");

        var removedCount = 0;
        var errors = new List<string>();

        foreach (var year in RevitYears)
        {
            var yearDir = Path.Combine(appDataAddins, year);
            if (!Directory.Exists(yearDir)) continue;

            // Remove THBIM.addin
            try
            {
                var addinFile = Path.Combine(yearDir, "THBIM.addin");
                if (File.Exists(addinFile)) File.Delete(addinFile);
            }
            catch (Exception ex) { errors.Add($"{year}: {ex.Message}"); }

            // Remove TH Tools folder
            try
            {
                var toolsDir = Path.Combine(yearDir, "TH Tools");
                if (Directory.Exists(toolsDir))
                {
                    Directory.Delete(toolsDir, true);
                    removedCount++;
                }
            }
            catch (Exception ex) { errors.Add($"{year}: {ex.Message}"); }
        }

        return (removedCount, errors);
    }

    /// <summary>
    /// Remove app settings, startup registry, and schedule self-deletion.
    /// </summary>
    public void RemoveApp()
    {
        // 1. Remove startup registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("THBIM AutoUpdate", false);
        }
        catch { }

        // 2. Remove settings folder
        try
        {
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "THBIM", "AutoUpdate");
            if (Directory.Exists(settingsDir))
                Directory.Delete(settingsDir, true);
        }
        catch { }

        // 3. Remove licensing cache
        try
        {
            var licDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "THBIM", "Licensing");
            if (Directory.Exists(licDir))
                Directory.Delete(licDir, true);
        }
        catch { }

        // 4. Create bat script to delete only the exe after app exits
        var currentExe = StartupService.CanonicalExePath;
        var batPath = Path.Combine(Path.GetTempPath(), $"thbim_uninstall_{Guid.NewGuid():N}.bat");

        var batContent = $"""
            @echo off
            echo THBIM AutoUpdate - Uninstalling...
            :wait
            tasklist /FI "PID eq {Environment.ProcessId}" 2>NUL | find "{Environment.ProcessId}" >NUL
            if %ERRORLEVEL%==0 (
                timeout /t 1 /nobreak >NUL
                goto wait
            )
            echo Removing app...
            del /f /q "{currentExe}" >NUL 2>&1
            del "%~f0" >NUL 2>&1
            """;

        File.WriteAllText(batPath, batContent);

        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true
        });
    }
}
