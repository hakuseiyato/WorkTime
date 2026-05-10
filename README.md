# WorkTime

作業時間を「ただ計る」だけじゃない、Windows 用の作業時間トラッカー。
指定したアプリの起動を検知して自動で計測を開始/停止し、月ごとの CSV ログとして残す。

## 状況 ⇒ 課題 ⇒ 解決

クリエイティブワークでは「Unity を立ち上げて Blender に切り替えて TouchDesigner で確認する」といった、複数アプリを跨ぐ作業時間を後追いで集計したい場面がある。
ストップウォッチ常駐は ON/OFF を忘れがちで実態と乖離し、Toggl 等の SaaS は外部送信が前提でローカル完結にならない。
WorkTime は **指定プロセスの起動検知** + **無操作自動一時停止** + **月次 CSV 出力** をスタンドアロンで提供する。

## 主な機能

- 監視対象プロセスを複数登録 → 起動検知で自動 ON、終了検知で自動 OFF
- 手動 ON/OFF トグル (自動より優先)
- 無操作時間 N 分超過で自動一時停止 (Win32 `GetLastInputInfo`)
- フリップ時計風 UI で「時 : 分 : 秒」を大きく表示 (HourFlow 参考)
- スコープ切替: 今日 / 今週 / 今月 / 全期間
- 目標時間に対する進捗バー
- プロジェクト別 (= 監視対象別) 累計表示
- タスクトレイ常駐、× ボタンでトレイ最小化
- 月ごとの CSV ログ (`data/logs/YYYY-MM.csv`)
- OS 起動時の自動起動 (HKCU\Run)

## ディレクトリ構成

```
WorkTime/
├─ WorkTime.sln
├─ src/
│  └─ WorkTime/                .NET 8 WPF プロジェクト本体
│     ├─ Models/                AppConfig / SessionRecord / ProjectSummary
│     ├─ Services/              ProcessMonitor / IdleDetector / TimeTracker / CsvLogger / ConfigStore / StartupRegistrar
│     ├─ ViewModels/            MainViewModel / RelayCommand / ObservableObject
│     ├─ Views/                 SettingsWindow & VM
│     ├─ Controls/              FlipCard
│     ├─ Resources/Theme.xaml   ダークテーマ (ArtNet Manager × HourFlow 配色)
│     ├─ App.xaml(.cs)          エントリ + タスクトレイ
│     └─ MainWindow.xaml(.cs)
├─ data/                        実行時に自動生成。git 管理外
│  ├─ config.json               アプリ設定
│  └─ logs/YYYY-MM.csv          月別ログ
└─ README.md
```

## ビルド

.NET 8 SDK が必要。

```powershell
dotnet build WorkTime.sln -c Release
```

`src\WorkTime\bin\Release\net8.0-windows\WorkTime.exe` が生成される。

## 実行

```powershell
dotnet run --project src/WorkTime/WorkTime.csproj
```

または上記 `WorkTime.exe` を直接実行。`--tray` 引数を付けるとトレイ最小化で起動する (自動起動向け)。

## 設定

メインウィンドウ右上「設定」から:

- 監視対象プロセス: プロセス名 (拡張子なし)、表示名、有効フラグを編集
- アイドル閾値 (分): 0 で無効
- 今日 / 今週 / 今月 の目標時間
- 閉じたらトレイへ最小化
- OS 起動時に自動起動

設定は `data/config.json` に保存される。

## CSV フォーマット

`data/logs/YYYY-MM.csv`、UTF-8 ヘッダ付き:

```
Date,StartTime,EndTime,DurationSec,ProjectKey,ProcessName,Source
2026-05-10,09:30:12,11:42:53,7961,Unity,Unity,auto
2026-05-10,13:15:00,14:00:00,2700,Manual,Manual,manual
```

`Source` は `auto` (プロセス検知) または `manual` (手動トグル)。

## 既知の制限

- プロセス名一致なので、同名プロセスは区別されない (例: `Unity` プロジェクト別追跡は不可)。必要なら `DisplayName` を分けて手動切替で運用する。
- 日付をまたいだセッションは 23:59:59 で一度フラッシュし、翌日 00:00 から再開する (集計を綺麗に保つため)。
- 高 DPI: PerMonitorV2 で動作。

## ライセンス

社内ツール。
