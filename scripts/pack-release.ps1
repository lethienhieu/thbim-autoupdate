# THBIM Revit Tools - Pack Release Script
# Chạy: powershell -ExecutionPolicy Bypass -File pack-release.ps1

param(
    [string]$Version = "1.0.0",
    [string]$SourceDir = "C:\ProgramData\Autodesk\Revit\TH Tools",
    [string]$OutputDir = "$PSScriptRoot\..\releases"
)

Write-Host "=== THBIM Pack Release ===" -ForegroundColor Yellow
Write-Host "Version: $Version"
Write-Host "Source:  $SourceDir"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$zipName = "THBIM-RevitTools.zip"
$zipPath = Join-Path $OutputDir $zipName

# Remove old zip if exists
if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

# Create ZIP
Write-Host "Packing $SourceDir -> $zipPath ..." -ForegroundColor Cyan
Compress-Archive -Path "$SourceDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

$size = (Get-Item $zipPath).Length / 1MB
Write-Host ""
Write-Host "Done! Created: $zipPath" -ForegroundColor Green
Write-Host "Size: $([math]::Round($size, 2)) MB"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Go to your GitHub repo -> Releases -> Create new release"
Write-Host "2. Tag: v$Version"
Write-Host "3. Drag & drop '$zipPath' into the release"
Write-Host "4. Publish release"
