using System.IO;
using System.IO.Compression;
using System.Net.Http;
using THBIM.AutoUpdate.Helpers;
using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.Services;

/// <summary>
/// Result of an update install operation.
/// </summary>
public record UpdateResult(
    bool Success,                   // true = all files copied successfully
    bool HasPending,                // true = some files locked, waiting for Revit to close
    string? Error,                  // hard failure message (download error, etc.)
    PendingContext? Pending = null   // context for retrying locked files
);

/// <summary>
/// Context to retry installing locked files after Revit closes.
/// </summary>
public record PendingContext(
    string TempExtractPath,
    List<(string Source, string Dest)> Files
);

public class UpdateService
{
    private static readonly HttpClient Http = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Revit version → .NET target mapping
    private static readonly Dictionary<string, string> RevitNetMap = new()
    {
        ["2021"] = "net48",
        ["2022"] = "net48",
        ["2023"] = "net48",
        ["2024"] = "net48",
        ["2025"] = "net8",
        ["2026"] = "net8",
    };

    static UpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("THBIM-AutoUpdate/1.0");
        Http.Timeout = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Detect which Revit versions are installed on this machine.
    /// </summary>
    private List<string> DetectInstalledRevitVersions()
    {
        var versions = new List<string>();
        var appDataAddins = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins");
        var programDataAddins = @"C:\ProgramData\Autodesk\Revit\Addins";

        foreach (var year in RevitNetMap.Keys)
        {
            if (Directory.Exists(Path.Combine(appDataAddins, year)) ||
                Directory.Exists(Path.Combine(programDataAddins, year)))
            {
                versions.Add(year);
            }
        }

        if (versions.Count == 0)
            versions.Add("2025");

        return versions;
    }

    public async Task<UpdateResult> DownloadAndInstallAsync(
        AddinConfig config,
        string downloadUrl,
        string newVersion,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken ct = default)
    {
        if (!await _lock.WaitAsync(0, ct))
            return new UpdateResult(false, false, "Another update is already in progress.");

        try
        {
            // Download to temp
            var tempZip = Path.Combine(Path.GetTempPath(), $"thbim_update_{Guid.NewGuid():N}.zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"thbim_extract_{Guid.NewGuid():N}");

            try
            {
                // Download ZIP
                using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloadedBytes += bytesRead;
                    progress?.Report((downloadedBytes, totalBytes));
                }
                fileStream.Close();

                // Extract to temp folder first
                Directory.CreateDirectory(tempExtract);
                ZipFile.ExtractToDirectory(tempZip, tempExtract, true);

                // Install to each detected Revit version, collecting locked files
                var pendingFiles = new List<(string Source, string Dest)>();
                var installedVersions = DetectInstalledRevitVersions();
                var appDataAddins = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins");

                foreach (var year in installedVersions)
                {
                    var targetDir = Path.Combine(appDataAddins, year);
                    Directory.CreateDirectory(targetDir);

                    var netTarget = RevitNetMap.GetValueOrDefault(year, "net8");

                    // Copy .addin file(s) from ZIP root
                    foreach (var file in Directory.GetFiles(tempExtract, "*.addin"))
                    {
                        var dest = Path.Combine(targetDir, Path.GetFileName(file));
                        CopyOrQueue(file, dest, pendingFiles);
                    }

                    // Copy the correct DLL set (net48 or net8) into TH Tools
                    var sourceToolsDir = Path.Combine(tempExtract, netTarget);
                    if (Directory.Exists(sourceToolsDir))
                    {
                        var destToolsDir = Path.Combine(targetDir, "TH Tools");
                        Directory.CreateDirectory(destToolsDir);

                        foreach (var file in Directory.GetFiles(sourceToolsDir, "*.*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(sourceToolsDir, file);
                            var dest = Path.Combine(destToolsDir, relativePath);
                            var destDir = Path.GetDirectoryName(dest)!;
                            Directory.CreateDirectory(destDir);

                            CopyOrQueue(file, dest, pendingFiles);
                        }
                    }
                }

                config.InstalledVersion = newVersion;

                // Cleanup ZIP always
                try { File.Delete(tempZip); } catch { }

                if (pendingFiles.Count > 0)
                {
                    // Keep tempExtract for retry later
                    return new UpdateResult(false, true, null,
                        new PendingContext(tempExtract, pendingFiles));
                }

                // All files copied — cleanup temp extract
                try { Directory.Delete(tempExtract, true); } catch { }
                return new UpdateResult(true, false, null);
            }
            catch (Exception)
            {
                // Cleanup on hard failure
                try { File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch { }
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            return new UpdateResult(false, false, "Update was cancelled.");
        }
        catch (Exception ex)
        {
            return new UpdateResult(false, false, ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Install pending files that were locked during the initial update.
    /// Called after Revit closes.
    /// </summary>
    public async Task<UpdateResult> InstallPendingAsync(PendingContext pending)
    {
        if (!await _lock.WaitAsync(0))
            return new UpdateResult(false, false, "Another update is already in progress.");

        try
        {
            var stillPending = new List<(string Source, string Dest)>();

            foreach (var (source, dest) in pending.Files)
            {
                CopyOrQueue(source, dest, stillPending);
            }

            // Cleanup temp extract
            try { if (Directory.Exists(pending.TempExtractPath)) Directory.Delete(pending.TempExtractPath, true); } catch { }

            if (stillPending.Count > 0)
                return new UpdateResult(false, false, $"Cannot overwrite {stillPending.Count} file(s) — still locked.");

            return new UpdateResult(true, false, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Try to copy a file. If locked, try fallback (move to .old then copy).
    /// If still fails, add to pending list instead of failing.
    /// </summary>
    private static void CopyOrQueue(string source, string dest, List<(string Source, string Dest)> pendingFiles)
    {
        try
        {
            File.Copy(source, dest, true);
        }
        catch (IOException)
        {
            // Fallback: move old file, then copy
            var backup = dest + ".old";
            try
            {
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(dest)) File.Move(dest, backup);
                File.Copy(source, dest, true);
                try { File.Delete(backup); } catch { }
            }
            catch
            {
                // Still locked — queue for later
                pendingFiles.Add((source, dest));
            }
        }
    }
}
