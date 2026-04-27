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

    // Files shipped by older builds that MUST be removed on update.
    // Matching is case-insensitive and supports wildcards via DirectoryInfo glob.
    // Anything here is either a legal liability (EPPlus AGPL, Microdesk icon) or
    // an orphan (icon files that the new ribbon no longer references).
    private static readonly string[] DeprecatedFileGlobs =
    [
        // EPPlus removed in addin v1.1.13 — legal: AGPL/commercial license
        "EPPlus.dll", "EPPlus.Interfaces.dll", "EPPlus.System.Drawing.dll",
        // Microdesk-named icon — IP risk
        "Resources/MEP/Microdesk.FlipMultiple32.png",
        // Entire MEP icon folder — addin v1.1.13 removed all ribbon icons
        "Resources/MEP",
        // Ribbon icons that the text-only ribbon no longer references.
        // Window UI still uses preview_dim_*.png + TH_*.png so those are NOT listed.
        "Resources/Arrange_16.png", "Resources/AutoPile_16.png", "Resources/BOQ_16.png",
        "Resources/BottomAlign_16.png", "Resources/BottomAlign_32.png", "Resources/Col_32.png",
        "Resources/ColorSplasher_16.png", "Resources/CombineParam_16.png", "Resources/DIM.png",
        "Resources/Droppanel_16.png", "Resources/Droppanel_32.png", "Resources/Floordrop_16.png",
        "Resources/GridBubble.png", "Resources/IDs_16.png", "Resources/LeftAlign_16.png",
        "Resources/LeftAlign_32.png", "Resources/LevelRehost_16.png", "Resources/OPENING.png",
        "Resources/OPENINGPRV.png", "Resources/ProSheet_32.png", "Resources/Profilter_16.png",
        "Resources/QTOPRO_32.png", "Resources/Rename_16.png", "Resources/RightAlign_16.png",
        "Resources/RightAlign_32.png", "Resources/SheetLink_32.png", "Resources/SplitPipe_16.png",
        "Resources/Splitcol_16.png", "Resources/StructureSync_32.png", "Resources/SyncParam_16.png",
        "Resources/TopAlign_16.png", "Resources/TopAlign_32.png", "Resources/Zone_16.png",
        "Resources/accept_16.png", "Resources/cloud_16.png", "Resources/dimensions.png",
        "Resources/dimensions.jpg", "Resources/hanger_16.png", "Resources/overlap_16.png",
        "Resources/select_16.png", "Resources/update_32.png",
    ];

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

                // Sweep deprecated files (EPPlus, Microdesk icon, orphaned ribbon
                // icons) from each addin folder so users upgrading from <1.1.13
                // no longer have stale, legally-problematic, or unreferenced files
                // sitting in their Revit addin directory.
                foreach (var year in installedVersions)
                {
                    var destToolsDir = Path.Combine(appDataAddins, year, "TH Tools");
                    PurgeDeprecatedFiles(destToolsDir);
                }

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

            // Now that Revit has released its lock, retry deprecated-file purge
            // on every TH Tools folder we just wrote into. Files that were
            // skipped during the initial install (because Revit was running)
            // can now be removed.
            var purgedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, dest) in pending.Files)
            {
                var root = FindAddinRoot(dest);
                if (root != null && purgedRoots.Add(root))
                    PurgeDeprecatedFiles(root);
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
    /// Walk up from a file path to find the "TH Tools" directory it lives in.
    /// Returns null if the path is not under a TH Tools folder.
    /// </summary>
    private static string? FindAddinRoot(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (string.Equals(Path.GetFileName(dir), "TH Tools", StringComparison.OrdinalIgnoreCase))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    /// Delete every file/folder listed in <see cref="DeprecatedFileGlobs"/> from
    /// <paramref name="addinRoot"/>. Best-effort: a locked file is skipped, not
    /// failed. Called after installing the new version to remove residue from
    /// older builds (EPPlus DLLs with AGPL licence, Microdesk-named icon,
    /// orphan ribbon icons no longer referenced).
    /// </summary>
    private static void PurgeDeprecatedFiles(string addinRoot)
    {
        if (string.IsNullOrWhiteSpace(addinRoot) || !Directory.Exists(addinRoot)) return;

        foreach (var rel in DeprecatedFileGlobs)
        {
            var path = Path.Combine(addinRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal); // clear read-only if any
                    File.Delete(path);
                }
            }
            catch
            {
                // Locked or in use — leave it; the next update attempt will retry.
                // A locked file is not a hard failure for the update itself.
            }
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
