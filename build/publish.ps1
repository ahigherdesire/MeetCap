# Publishes MeetCap as a self-contained x64 app so end users need nothing pre-installed.
# Output: <repo>\artifacts\publish
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\MeetCap\MeetCap.csproj"
$outDir = Join-Path $root "artifacts\publish"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

Write-Host "Publishing MeetCap ($Configuration, self-contained win-x64)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit $LASTEXITCODE)." }

# ScreenRecorderLib is mixed-mode C++/CLI and links the Visual C++ runtime
# (MSVCP140 / VCRUNTIME140 / CONCRT140). The self-contained .NET publish does NOT
# include these, so we ship them app-local — copied next to the exe, where they take
# load priority. This makes the folder fully portable: no VC++ redist required on
# the target machine, and no admin/installer step for the runtime.
$vcRuntime = @(
    'msvcp140.dll', 'msvcp140_1.dll', 'msvcp140_2.dll',
    'vcruntime140.dll', 'vcruntime140_1.dll',
    'concrt140.dll', 'vccorlib140.dll'
)
$copied = 0; $missing = @()
foreach ($name in $vcRuntime) {
    $src = Join-Path $env:WINDIR "System32\$name"
    if (Test-Path $src) { Copy-Item $src -Destination $outDir -Force; $copied++ }
    else { $missing += $name }
}
Write-Host "Bundled $copied Visual C++ runtime DLL(s) app-local." -ForegroundColor DarkCyan
if ($missing.Count -gt 0) {
    Write-Warning ("Could not find these VC++ runtime DLLs on this build machine: " +
        ($missing -join ', ') + ". Install the VC++ 2015-2022 x64 redistributable and re-run.")
}

# Ship the license and third-party notices alongside the app.
foreach ($doc in @("LICENSE.txt", "THIRD-PARTY-NOTICES.txt")) {
    $src = Join-Path $root $doc
    if (Test-Path $src) { Copy-Item $src -Destination $outDir -Force }
}

Write-Host "Published to: $outDir" -ForegroundColor Green
Get-ChildItem $outDir -Filter "MeetCap.exe" | ForEach-Object { Write-Host ("  " + $_.FullName) }
