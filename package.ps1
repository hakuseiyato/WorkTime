# WorkTime - distribution packaging script
#
# Produces  WorkTime_v<version>.zip  in the repository root containing:
#   WorkTime.exe         (the single-file self-contained app, freshly published)
#   README.txt           (Japanese quickstart for the recipient)
#   data/.keep           (placeholder so app's data folder pre-exists)
#
# Usage:
#   .\package.ps1
#   .\package.ps1 -Version 0.3.2     # override version string
#   .\package.ps1 -SkipPublish       # reuse existing WorkTime.exe at root

param(
    [string]$Version = '',
    [switch]$SkipPublish,
    [switch]$Open
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Resolve version: arg > csproj <Version>
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csproj = Get-ChildItem -Path (Join-Path $root '_Dev') -Recurse -Filter 'WorkTime.csproj' | Select-Object -First 1
    if ($csproj) {
        $xml = [xml](Get-Content $csproj.FullName)
        $v = $xml.Project.PropertyGroup.Version
        if ($v) { $Version = ($v | Select-Object -First 1).ToString().Trim() }
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = '0.0.0' }
Write-Host "==> WorkTime distribution package, version $Version" -ForegroundColor Cyan

# 1) Build the exe (single-file self-contained) unless skipped
if (-not $SkipPublish) {
    Write-Host '==> Running publish.ps1' -ForegroundColor Cyan
    & (Join-Path $root 'publish.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'publish.ps1 failed' }
}

$exe = Join-Path $root 'WorkTime.exe'
if (-not (Test-Path $exe)) {
    throw "WorkTime.exe not found at $exe (run without -SkipPublish or run publish.ps1 first)"
}

# 2) Stage files
$stage = Join-Path $root '_package_tmp'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory $stage | Out-Null

Copy-Item $exe $stage

# Pre-create data/ so the app finds it on first launch
$dataDir = Join-Path $stage 'data'
New-Item -ItemType Directory $dataDir | Out-Null
'WorkTime data directory placeholder' | Out-File (Join-Path $dataDir '.keep') -Encoding UTF8

# Write a short README in Japanese (UTF-8 BOM for Notepad friendliness)
$readmeLines = @(
    'WorkTime',
    '========',
    'Windows 用の作業時間トラッカー。',
    '',
    '* 起動方法: WorkTime.exe をダブルクリック',
    '* 設定とログは exe と同じ階層の data フォルダに保存されます',
    '    - data/config.json         設定 (監視対象プロセス、目標時間 など)',
    '    - data/logs/YYYY-MM.csv    月別ログ',
    '* 引継ぎ: 旧環境の data フォルダを丸ごと新環境にコピーすれば',
    '          設定もログも復元できます',
    '',
    'タスクトレイに常駐します。',
    'コンパクト表示 (タイトルバーの 矩形ボタン) と最前面固定 (ピンボタン) を',
    '組み合わせるとデスクトップ常設ウィジェットとして使えます。',
    '',
    '集計エクスポート: メイン画面右下のボタンから',
    '期間とプロジェクトを選んで CSV 出力できます。'
)
$readme = $readmeLines -join "`r`n"
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $readme, $utf8Bom)

# 3) Zip
$zip = Join-Path $root ("WorkTime_v$Version.zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

Remove-Item $stage -Recurse -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host '==> Done.' -ForegroundColor Green
Write-Host ("  $zip  ($mb MB)") -ForegroundColor Green

if ($Open) { Start-Process explorer.exe (Split-Path $zip -Parent) }
