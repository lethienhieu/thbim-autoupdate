# THBIM AutoUpdate — Workflow & Notes

## Project Info
- **Repo:** https://github.com/lethienhieu/thbim-autoupdate
- **Repo dung de:** Chi upload exe vao GitHub Releases. KHONG push source code len repo.
- **Source code:** `E:\THBIM-CODE\2025\THBIM-AutoUpdate\src\`
- **Framework:** .NET 8.0 WPF, self-contained single-file exe (x64)
- **Canonical exe path:** `%AppData%\THBIM\AutoUpdate\THBIM.AutoUpdate.exe`

## Release Workflow (CHI UPLOAD EXE, KHONG PUSH SOURCE)

### Buoc 1: Update version
Sua version o 2 cho:
- `src/THBIM.AutoUpdate/THBIM.AutoUpdate.csproj` → `<Version>`, `<FileVersion>`, `<AssemblyVersion>`
- `src/THBIM.AutoUpdate/ViewModels/MainViewModel.cs` → `AppVersion = "x.x.x"`

### Buoc 2: Build & Publish
```bash
cd "E:\THBIM-CODE\2025\THBIM-AutoUpdate"
dotnet publish src/THBIM.AutoUpdate/THBIM.AutoUpdate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
Output: `publish/THBIM.AutoUpdate.exe` (~155 MB)

### Buoc 3: Tao tag (KHONG commit source len main)
```bash
cd "E:\THBIM-CODE\2025\THBIM-AutoUpdate"
git tag v1.x.x
git push origin v1.x.x
```

### Buoc 4: Tao GitHub Release + Upload exe
```bash
# Lay token tu git credential manager
TOKEN=$(echo "url=https://github.com" | git credential fill 2>/dev/null | grep password | cut -d= -f2)

# Tao release
curl -s -X POST "https://api.github.com/repos/lethienhieu/thbim-autoupdate/releases" \
  -H "Authorization: token $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tag_name": "v1.x.x",
    "name": "v1.x.x",
    "body": "## Changelog\n- ...",
    "draft": false,
    "prerelease": false
  }' > /tmp/release_response.json

# Lay release ID tu response
# cat /tmp/release_response.json | grep '"id"' (lay so dau tien)

# Upload exe (thay RELEASE_ID)
curl -s -X POST "https://uploads.github.com/repos/lethienhieu/thbim-autoupdate/releases/RELEASE_ID/assets?name=THBIM.AutoUpdate.exe" \
  -H "Authorization: token $TOKEN" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @"publish/THBIM.AutoUpdate.exe"
```

## Architecture Overview

### Services
| Service | File | Chuc nang |
|---|---|---|
| StartupService | `Services/StartupService.cs` | Registry auto-start, canonical path |
| UpdateService | `Services/UpdateService.cs` | Download ZIP, extract, copy DLLs (hot-reload) |
| SelfUpdateService | `Services/SelfUpdateService.cs` | Tu cap nhat exe qua bat script |
| SettingsService | `Services/SettingsService.cs` | Load/save JSON settings |
| UninstallService | `Services/UninstallService.cs` | Go cai dat app + addins |
| LicenseService | `Services/LicenseService.cs` | Xac thuc nguoi dung |
| GitHubReleaseService | `Services/GitHubReleaseService.cs` | Query GitHub API |

### Update Flow (v1.0.9+)
```
Download ZIP → Extract → Try copy tung file:
  Copy duoc  → done (hot-reload, bam Reload trong Revit)
  IOException → them vao pendingFiles, giu temp folder

Khong co pending → full success → cleanup temp
Co pending → partial success + watch Revit tat → copy pending → full success
```

### Exe Location (v1.0.9+)
- Lan dau chay tu bat ky dau → tu copy vao `%AppData%\THBIM\AutoUpdate\` → relaunch
- Registry startup luon tro den canonical path
- Self-update copy exe moi vao canonical path
- Uninstall xoa dung canonical path

### Settings
- File: `%AppData%\THBIM\AutoUpdate\settings.json`
- Default: `startWithWindows: true`, `autoUpdate: true`, `checkIntervalMinutes: 1440`

### Self-Update Mechanism
- Repo: `lethienhieu/thbim-autoupdate` (GitHub Releases)
- App check self-update truoc khi check tool update
- Download exe moi → bat script doi app tat → copy vao canonical path → restart voi --minimized
