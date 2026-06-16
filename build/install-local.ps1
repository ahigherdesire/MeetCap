# Installs MeetCap for the current user so it shows up in the Start menu / Windows Search
# (type "meetcap"). No admin required. Run build\publish.ps1 first.
$ErrorActionPreference = "Stop"
$root      = Split-Path -Parent $PSScriptRoot
$publish   = Join-Path $root "artifacts\publish"
$dest      = Join-Path $env:LOCALAPPDATA "Programs\MeetCap"
$exe       = Join-Path $dest "MeetCap.exe"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\MeetCap.lnk"

if (-not (Test-Path (Join-Path $publish "MeetCap.exe"))) {
    throw "Publish output not found. Run build\publish.ps1 first."
}

# Stop any running instance so files aren't locked.
Get-Process MeetCap -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Copy the self-contained app into a stable per-user install folder.
Write-Host "Installing to $dest ..." -ForegroundColor Cyan
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $publish "*") -Destination $dest -Recurse -Force

# Create the Start menu shortcut (this is what Windows Search indexes).
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($startMenu)
$sc.TargetPath       = $exe
$sc.WorkingDirectory = $dest
$sc.IconLocation     = $exe
$sc.Description       = "MeetCap - meeting recorder"
$sc.Save()

Write-Host "Installed. Type 'meetcap' in the Start menu to launch it." -ForegroundColor Green
Write-Host "  App:      $exe"
Write-Host "  Shortcut: $startMenu"
