# WorkTime - single-exe publish script
#
# What it does:
#   1. Find the latest _Dev/v* folder and publish its WorkTime.csproj
#      as a single-file self-contained exe.
#   2. Copy the resulting WorkTime.exe to the repository root.
#   3. Clean up the intermediate folder unless -KeepTmp is specified.
#
# Usage:
#   .\publish.ps1
#   .\publish.ps1 -KeepTmp
#   .\publish.ps1 -Open

param(
    [switch]$KeepTmp,
    [switch]$Open
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Pick the newest v* folder under _Dev/
$devRoot = Join-Path $root '_Dev'
$versionDir = Get-ChildItem $devRoot -Directory |
    Where-Object { $_.Name -like 'v*' } |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $versionDir) {
    throw 'No v* folder found under _Dev/'
}
$proj = Join-Path $versionDir.FullName 'src\WorkTime\WorkTime.csproj'
if (-not (Test-Path $proj)) {
    throw ('csproj not found: ' + $proj)
}

$tmp    = Join-Path $root '_publish_tmp'
$outExe = Join-Path $root 'WorkTime.exe'

Write-Host "==> Source: $proj" -ForegroundColor Cyan

if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Path $tmp | Out-Null

Write-Host '==> Publishing single-file self-contained exe...' -ForegroundColor Cyan
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $tmp

if ($LASTEXITCODE -ne 0) {
    Write-Host 'publish failed.' -ForegroundColor Red
    exit 1
}

$tmpExe = Join-Path $tmp 'WorkTime.exe'
if (-not (Test-Path $tmpExe)) {
    throw ('Publish output is missing: ' + $tmpExe)
}

try {
    Copy-Item $tmpExe $outExe -Force
} catch {
    Write-Host 'Failed to overwrite WorkTime.exe. Close the running WorkTime and try again.' -ForegroundColor Yellow
    Write-Host ('  detail: ' + $_.Exception.Message) -ForegroundColor Yellow
    exit 1
}

if (-not $KeepTmp) {
    Remove-Item $tmp -Recurse -Force
}

$size = [math]::Round((Get-Item $outExe).Length / 1MB, 1)
Write-Host ''
Write-Host '==> Done.' -ForegroundColor Green
Write-Host ('  ' + $outExe + '  (' + $size + ' MB)') -ForegroundColor Green

if ($Open) {
    Start-Process explorer.exe $root
}
