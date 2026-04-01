using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace THBIM.AutoUpdate.Services;

// Self-update always targets the canonical exe path: %AppData%\THBIM\AutoUpdate\THBIM.AutoUpdate.exe

public class SelfUpdateService
{
    private static readonly HttpClient Http = new();

    static SelfUpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("THBIM-AutoUpdate/1.0");
        Http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<bool> UpdateSelfAsync(string downloadUrl, IProgress<(long downloaded, long total)>? progress = null, CancellationToken ct = default)
    {
        var canonicalExe = StartupService.CanonicalExePath;
        var tempExe = Path.Combine(Path.GetTempPath(), $"THBIM.AutoUpdate_new_{Guid.NewGuid():N}.exe");
        var batPath = Path.Combine(Path.GetTempPath(), $"thbim_selfupdate_{Guid.NewGuid():N}.bat");

        // 1. Download new exe to temp
        using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloadedBytes += bytesRead;
            progress?.Report((downloadedBytes, totalBytes));
        }
        fileStream.Close();

        // 2. Create bat script that waits, replaces, restarts
        var batContent = $"""
            @echo off
            echo THBIM AutoUpdate - Self Update
            echo Waiting for app to close...
            :wait
            tasklist /FI "PID eq {Environment.ProcessId}" 2>NUL | find "{Environment.ProcessId}" >NUL
            if %ERRORLEVEL%==0 (
                timeout /t 1 /nobreak >NUL
                goto wait
            )
            echo Replacing exe...
            copy /Y "{tempExe}" "{canonicalExe}" >NUL
            if %ERRORLEVEL%==0 (
                echo Starting new version...
                start "" "{canonicalExe}" --minimized
                del "{tempExe}" >NUL 2>&1
                del "%~f0" >NUL 2>&1
            ) else (
                echo Failed to replace exe!
                pause
            )
            """;

        await File.WriteAllTextAsync(batPath, batContent, ct);

        // 3. Launch bat script (hidden window)
        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true
        });

        // 4. Exit current app — bat script will replace and restart
        return true;
    }
}
