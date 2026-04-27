# THBIM Manual Cleanup Script
# ===========================
#
# Removes all THBIM data from this Windows account, including residue from
# old (pre-v2.0) installs that may contain unredacted personal data.
#
# Use this if:
#   • You uninstalled THBIM before v1.2.0 was released
#   • You want to fully wipe all THBIM data including caches
#   • A privacy/IT audit requires you to confirm data has been removed
#
# Run from PowerShell:
#   PS> .\THBIM-Cleanup.ps1
#
# Or right-click → Run with PowerShell. The script asks for confirmation
# before deleting anything. No admin rights required (deletes only files
# in the current user's profile).
#
# WHAT IS DELETED
#   1. THBIM addin folder for every Revit version (2021–2026):
#        %APPDATA%\Autodesk\Revit\Addins\<year>\TH Tools\
#        %APPDATA%\Autodesk\Revit\Addins\<year>\THBIM.addin
#   2. Licensing data:
#        %LOCALAPPDATA%\THBIM\Licensing\
#   3. AutoUpdate app settings:
#        %APPDATA%\THBIM\AutoUpdate\
#   4. Startup registry entry (HKCU Run key)
#   5. Optional: AutoUpdate executable (asked separately)
#
# WHAT IS NOT DELETED
#   • Your Revit projects, families, sheets — never touched.
#   • Server-side account data — request deletion by emailing the publisher
#     (see PRIVACY.md) with subject "Delete my account".
#
# License: Proprietary © 2026 THBIM. See LICENSE.

$ErrorActionPreference = 'SilentlyContinue'

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  THBIM Manual Cleanup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Confirm with user before deleting anything
$response = Read-Host "This will permanently delete ALL THBIM data on this account. Continue? (y/N)"
if ($response -ne 'y' -and $response -ne 'Y') {
    Write-Host "Cancelled. Nothing was deleted." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
$deleted = 0
$skipped = 0

function Remove-PathReport {
    param([string]$Path, [string]$Label)
    if (Test-Path -LiteralPath $Path) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            Write-Host "  [DEL] $Label" -ForegroundColor Green
            return 1
        }
        catch {
            Write-Host "  [SKIP] $Label  ($($_.Exception.Message))" -ForegroundColor Yellow
            return 0
        }
    }
    return 0
}

# ---- 1. Revit addin folders for every supported Revit version ----
Write-Host "1. Revit addin folders..." -ForegroundColor White
$revitYears = @('2021','2022','2023','2024','2025','2026')
$appDataAddins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'

foreach ($year in $revitYears) {
    $yearDir = Join-Path $appDataAddins $year
    if (-not (Test-Path -LiteralPath $yearDir)) { continue }

    $addinFile = Join-Path $yearDir 'THBIM.addin'
    $deleted += Remove-PathReport -Path $addinFile -Label "Revit $year THBIM.addin"

    $toolsDir = Join-Path $yearDir 'TH Tools'
    $deleted += Remove-PathReport -Path $toolsDir -Label "Revit $year TH Tools/"
}

# ---- 2. Licensing cache (session, log, consent stamp, machine binding) ----
Write-Host ""
Write-Host "2. Licensing cache..." -ForegroundColor White
$licDir = Join-Path $env:LOCALAPPDATA 'THBIM\Licensing'
$deleted += Remove-PathReport -Path $licDir -Label '%LOCALAPPDATA%\THBIM\Licensing\'

# ---- 3. AutoUpdate settings ----
Write-Host ""
Write-Host "3. AutoUpdate settings..." -ForegroundColor White
$auSettings = Join-Path $env:APPDATA 'THBIM\AutoUpdate'
$deleted += Remove-PathReport -Path $auSettings -Label '%APPDATA%\THBIM\AutoUpdate\'

# Remove parent THBIM folders if now empty
foreach ($parent in @((Join-Path $env:LOCALAPPDATA 'THBIM'), (Join-Path $env:APPDATA 'THBIM'))) {
    if ((Test-Path -LiteralPath $parent) -and (-not (Get-ChildItem -LiteralPath $parent -Force))) {
        Remove-Item -LiteralPath $parent -Force
        Write-Host "  [DEL] $parent (now empty)" -ForegroundColor Green
        $deleted++
    }
}

# ---- 4. Startup registry entry (HKCU Run key, no admin needed) ----
Write-Host ""
Write-Host "4. Startup registry entry..." -ForegroundColor White
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
try {
    $val = Get-ItemProperty -Path $runKey -Name 'THBIM AutoUpdate' -ErrorAction Stop
    Remove-ItemProperty -Path $runKey -Name 'THBIM AutoUpdate' -ErrorAction Stop
    Write-Host '  [DEL] HKCU\...\Run\THBIM AutoUpdate' -ForegroundColor Green
    $deleted++
}
catch { Write-Host '  [N/A] No startup entry found' -ForegroundColor DarkGray }

# ---- 5. AutoUpdate executable (asked separately) ----
Write-Host ""
$canonicalExe = Join-Path $env:LOCALAPPDATA 'THBIM\AutoUpdate\THBIM.AutoUpdate.exe'
if (Test-Path -LiteralPath $canonicalExe) {
    Write-Host "5. AutoUpdate executable was found at:" -ForegroundColor White
    Write-Host "   $canonicalExe" -ForegroundColor Gray
    $r2 = Read-Host '   Delete it as well? (y/N)'
    if ($r2 -eq 'y' -or $r2 -eq 'Y') {
        $deleted += Remove-PathReport -Path $canonicalExe -Label 'AutoUpdate exe'
    } else {
        Write-Host '   Skipped.' -ForegroundColor Yellow
    }
}

# ---- Summary ----
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done. Deleted $deleted item(s)." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Reminder: server-side account data is NOT removed by this script." -ForegroundColor Yellow
Write-Host "To request server-side erasure under GDPR Art. 17, email the" -ForegroundColor Yellow
Write-Host "publisher with subject 'Delete my account'. See PRIVACY.md." -ForegroundColor Yellow
Write-Host ""
