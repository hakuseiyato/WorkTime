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
    'Windows 用の作業時間トラッカー。対象アプリの起動を検知して自動で計測し、',
    '月ごとの CSV に残します。タイムラプス録画も同じアプリで行えます。',
    '',
    '■ 起動方法',
    '  WorkTime.exe をダブルクリック。タスクトレイに常駐します。',
    '',
    '■ 保存先',
    '  設定とログは exe と同じ階層の data フォルダに保存されます。',
    '    - data/config.json              設定',
    '    - data/logs/YYYY-MM.csv         月別のセッションログ',
    '    - data/logs/markers-YYYY-MM.csv 打刻マーカー',
    '  引継ぎ: 旧環境の data フォルダを丸ごとコピーすれば設定もログも復元できます。',
    '',
    '■ 監視対象の登録',
    '  設定 →「監視対象プロセス」の「起動中のアプリから選択...」から、',
    '  今開いているアプリを選ぶだけで登録できます。プロセス名の手入力は不要です。',
    '  選択はアプリ本体に紐づくので、次回以降そのアプリを起動するたびに計測されます。',
    '  フォルダ単位で案件を分けたい場合は「監視フォルダ」に登録してください。',
    '',
    '■ メモと打刻',
    '  メイン画面のメモ欄に「今から何をする」を書いておくと、',
    '  そのセッションの記録に残ります。計測中でも書き換えられます。',
    '  「打刻」ボタンでその瞬間のマーカーを残せます (作業時間には影響しません)。',
    '',
    '■ タイムラプス録画 (既定では OFF)',
    '  設定 →「録画」で有効にすると、計測の開始と同時に録画が始まります。',
    '  対象アプリが載っているモニタ全体を撮ります。アイドル中は一時停止し、',
    '  セッション終了時に約 75 秒の早回しクリップを自動生成します。',
    '  別途 ffmpeg が必要です  (winget install Gyan.FFmpeg)。',
    '  保存先は設定で変更できます。既定のままだと動かない環境があります。',
    '',
    '■ その他',
    '  コンパクト表示 (タイトルバーの矩形ボタン) と最前面固定 (ピンボタン) を',
    '  組み合わせるとデスクトップ常設ウィジェットとして使えます。',
    '  集計エクスポート: メイン画面右下のボタンから期間とプロジェクトを選んで CSV 出力。'
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
