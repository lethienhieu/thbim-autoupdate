# THBIM Revit Tools - Pack Release Script (v1.0.1+)
# Usage: powershell -ExecutionPolicy Bypass -File pack.ps1

$tempDir = "E:\THBIM-CODE\2025\THBIM-AutoUpdate\releases\temp_pack"
$zipPath = "E:\THBIM-CODE\2025\THBIM-AutoUpdate\releases\THBIM-RevitTools.zip"
$manifestSrc = "E:\THBIM-CODE\2025\THBIM-AutoUpdate\releases\manifest.json"

# Source paths
$coreNet48 = "E:\THBIM-CODE\2025\CODE\THBIM\THBIM_Core\bin\Release\net48"
$coreNet8  = "E:\THBIM-CODE\2025\CODE\THBIM\THBIM_Core\bin\Release\net8.0-windows"
$licNet48  = "E:\THBIM-CODE\2025\CODE\THBIM\Licensing\bin\Release\net48"
$licNet8   = "E:\THBIM-CODE\2025\CODE\THBIM\Licensing\bin\Release\net8.0-windows"

# Addin template
$addinTemplate = "E:\THBIM-CODE\2025\THBIM-AutoUpdate\releases\addin-templates\THBIM.addin"

# Shared resources (icons, rfa families)
$resourcesDir = "E:\THBIM-CODE\2025\CODE\THBIM\THBIM_Core\Resources"
$rfaDir = "E:\THBIM-CODE\2025\CODE\THBIM\THBIM_Core\rfa"

Write-Host "=== THBIM Pack Release v1.0.1 ===" -ForegroundColor Yellow

# Clean temp
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null
New-Item -ItemType Directory -Path "$tempDir\net48" | Out-Null
New-Item -ItemType Directory -Path "$tempDir\net8" | Out-Null

# Copy net48 DLLs (Revit 2021-2024)
Write-Host "Copying net48 DLLs (Revit 2021-2024)..." -ForegroundColor Cyan
Copy-Item "$coreNet48\*" "$tempDir\net48\" -Recurse -Force
# Copy Licensing DLL (overwrite if exists)
Copy-Item "$licNet48\THBIM.Licensing.dll" "$tempDir\net48\" -Force
$net48Count = (Get-ChildItem "$tempDir\net48" -File).Count
Write-Host "  $net48Count files" -ForegroundColor Green

# Copy net8 DLLs (Revit 2025-2026)
Write-Host "Copying net8 DLLs (Revit 2025-2026)..." -ForegroundColor Cyan
Copy-Item "$coreNet8\*" "$tempDir\net8\" -Recurse -Force
# Copy Licensing DLL (overwrite if exists)
Copy-Item "$licNet8\THBIM.Licensing.dll" "$tempDir\net8\" -Force
$net8Count = (Get-ChildItem "$tempDir\net8" -File).Count
Write-Host "  $net8Count files" -ForegroundColor Green

# Copy Resources and rfa to BOTH net48 and net8
Write-Host "Copying Resources (icons)..." -ForegroundColor Cyan
if (Test-Path $resourcesDir) {
    Copy-Item $resourcesDir "$tempDir\net48\Resources" -Recurse -Force
    Copy-Item $resourcesDir "$tempDir\net8\Resources" -Recurse -Force
    $resCount = (Get-ChildItem $resourcesDir -File).Count
    Write-Host "  $resCount icon files" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Resources folder not found!" -ForegroundColor Red
}

Write-Host "Copying rfa (Revit families)..." -ForegroundColor Cyan
if (Test-Path $rfaDir) {
    Copy-Item $rfaDir "$tempDir\net48\rfa" -Recurse -Force
    Copy-Item $rfaDir "$tempDir\net8\rfa" -Recurse -Force
    $rfaCount = (Get-ChildItem $rfaDir -File).Count
    Write-Host "  $rfaCount family files" -ForegroundColor Green
} else {
    Write-Host "  WARNING: rfa folder not found!" -ForegroundColor Red
}

# Copy .addin file to ZIP root
Write-Host "Copying .addin file..." -ForegroundColor Cyan
if (Test-Path $addinTemplate) {
    Copy-Item $addinTemplate $tempDir
    Write-Host "  + THBIM.addin" -ForegroundColor Green
} else {
    Write-Host "  WARNING: THBIM.addin template not found!" -ForegroundColor Red
}

# Copy manifest.json
if (Test-Path $manifestSrc) {
    Copy-Item $manifestSrc $tempDir
    Write-Host "  + manifest.json" -ForegroundColor Green
}

# Remove old zip
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Create ZIP
Write-Host "Creating ZIP..." -ForegroundColor Cyan
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force

# Cleanup
Remove-Item $tempDir -Recurse -Force

$file = Get-Item $zipPath
$sizeMB = [math]::Round($file.Length / 1MB, 2)
Write-Host ""
Write-Host "Done! Created: $zipPath" -ForegroundColor Green
Write-Host "Size: $sizeMB MB"
Write-Host ""
Write-Host "ZIP structure:" -ForegroundColor Yellow
Write-Host "  THBIM.addin"
Write-Host "  manifest.json"
Write-Host "  net48\  (THBIM.dll + THBIM.Licensing.dll + Resources\ + rfa\ + deps) -> Revit 2021-2024"
Write-Host "  net8\   (THBIM.dll + THBIM.Licensing.dll + Resources\ + rfa\ + deps) -> Revit 2025-2026"
