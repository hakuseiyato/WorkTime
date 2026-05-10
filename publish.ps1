# WorkTime — 単一 exe ビルドスクリプト
# dist\WorkTime.exe 1 本だけを出力する。.NET ランタイム同梱。
#
# 使い方:
#   .\publish.ps1
#   .\publish.ps1 -Open    # ビルド後に dist フォルダを開く

param(
    [switch]$Open
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\WorkTime\WorkTime.csproj'
$dist = Join-Path $root 'dist'

Write-Host "==> Cleaning dist/" -ForegroundColor Cyan
if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}
New-Item -ItemType Directory -Path $dist | Out-Null

Write-Host "==> Publishing single-file self-contained exe..." -ForegroundColor Cyan
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $dist

if ($LASTEXITCODE -ne 0) {
    Write-Host "publish failed." -ForegroundColor Red
    exit 1
}

# .pdb など余計なものは消す
Get-ChildItem $dist -File | Where-Object { $_.Extension -in '.pdb','.xml' } | Remove-Item -Force

Write-Host ""
Write-Host "==> Done." -ForegroundColor Green
$exe = Join-Path $dist 'WorkTime.exe'
if (Test-Path $exe) {
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ("  {0}  ({1} MB)" -f $exe, $size) -ForegroundColor Green
}

if ($Open) {
    Start-Process explorer.exe $dist
}
