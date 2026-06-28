# Builds the self-contained app and compiles the Inno Setup installer.
# Requires Inno Setup 6 (iscc.exe). Install with: winget install JRSoftware.InnoSetup
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# 1) Publish the app.
& (Join-Path $PSScriptRoot "publish.ps1")

# 2) Locate iscc.exe.
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    foreach ($c in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}
if (-not $iscc) {
    throw "Inno Setup (iscc.exe) not found. Install it: winget install JRSoftware.InnoSetup"
}

# 3) Compile the installer.
$iss = Join-Path $root "installer\MeetCap.iss"
Write-Host "Compiling installer with $iscc ..." -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed (exit $LASTEXITCODE)." }

Write-Host "Installer created in: $(Join-Path $root 'artifacts\installer')" -ForegroundColor Green
