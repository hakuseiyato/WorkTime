# WorkTime

作業時間を「ただ計る」だけじゃない、Windows 用の作業時間トラッカー。
指定したアプリの起動を検知して自動で計測を開始/停止し、月ごとの CSV ログとして残す。

## 状況 ⇒ 課題 ⇒ 解決

クリエイティブワークでは「Unity を立ち上げて Blender に切り替えて TouchDesigner で確認する」といった、複数アプリを跨ぐ作業時間を後追いで集計したい場面がある。
ストップウォッチ常駐は ON/OFF を忘れがちで実態と乖離し、Toggl 等の SaaS は外部送信が前提でローカル完結にならない。
WorkTime は **指定プロセスの起動検知** + **無操作自動一時停止** + **月次 CSV 出力** をスタンドアロンで提供する。

## 使い方

### 起動

ルート直下の **`WorkTime.exe`** をダブルクリック。
.NET ランタイム同梱の自己完結 exe なので、配布先 PC への SDK インストール不要。

```
WorkTime/
├── WorkTime.exe        ← これをダブルクリックで起動
├── README.md
├── publish.ps1         ← exe を再生成するときだけ使う
├── _Dev/               ← ソース (普段触らない)
│   └── v0.2/
└── _old/               ← 旧バージョン保管
```

### 引数

- `--tray` … トレイ最小化で起動 (OS 自動起動向け)

二重起動防止: 起動中の WorkTime がある状態で `WorkTime.exe` を再度起動すると、既存ウィンドウが前面化される。

## 主な機能

- 監視対象プロセスを複数登録 → 起動検知で自動 ON、終了検知で自動 OFF
- 手動 ON/OFF トグル (自動より優先)
- 無操作時間 N 分超過で自動一時停止 (Win32 `GetLastInputInfo`)
- フリップ時計風 UI で「時 : 分 : 秒」を大きく表示
- スコープ切替: 今日 / 今週 / 今月 / 全期間
- 目標時間に対する進捗バー
- プロジェクト別 (= 監視対象別) 累計表示
- タスクトレイ常駐、× ボタンでトレイ最小化、ツールチップに現在時間ライブ表示
- 月ごとの CSV ログ (`data/logs/YYYY-MM.csv`)
- OS 起動時の自動起動 (HKCU\Run)

## 設定

メインウィンドウ右上「設定」から:

- 監視対象プロセス: プロセス名 (拡張子なし)、表示名、有効フラグを編集
- アイドル閾値 (分): 0 で無効
- 今日 / 今週 / 今月 の目標時間
- 閉じたらトレイへ最小化
- OS 起動時に自動起動

設定は `data/config.json`、ログは `data/logs/YYYY-MM.csv` に保存される。
どちらも `WorkTime.exe` と同じフォルダ直下に作成される。

## CSV フォーマット

`data/logs/YYYY-MM.csv`、UTF-8 ヘッダ付き:

```
Date,StartTime,EndTime,DurationSec,ProjectKey,ProcessName,Source
2026-05-10,09:30:12,11:42:53,7961,Unity,Unity,auto
2026-05-10,13:15:00,14:00:00,2700,Manual,Manual,manual
```

`Source` は `auto` (プロセス検知) または `manual` (手動トグル)。

## exe を作り直す (開発者向け)

.NET 8 SDK が必要。

```powershell
.\publish.ps1
# 出力: WorkTime.exe (約 70MB, 単一ファイル)
```

`-KeepTmp` で中間ファイル保持、`-Open` で完了後にエクスプローラを開く。

`_publish_tmp/` は中間生成物用。`bin/` `obj/` 等と合わせ git 管理外。

## ディレクトリ詳細

```
_Dev/v0.2/
├─ WorkTime.sln
└─ src/WorkTime/                .NET 8 WPF プロジェクト本体
   ├─ Models/                   AppConfig / SessionRecord / ProjectSummary
   ├─ Services/                 ProcessMonitor / IdleDetector / TimeTracker / CsvLogger /
   │                            ConfigStore / StartupRegistrar / SingleInstanceSignal
   ├─ ViewModels/               MainViewModel / RelayCommand / ObservableObject
   ├─ Views/                    SettingsWindow & VM
   ├─ Controls/                 FlipCard
   ├─ Resources/Theme.xaml      ダークテーマ (ArtNet Manager 風シアン/ティール)
   ├─ App.xaml(.cs)             エントリ + タスクトレイ + 二重起動防止
   └─ MainWindow.xaml(.cs)
```

## 既知の制限

- プロセス名一致なので、同名プロセスは区別されない (例: `Unity` プロジェクト別追跡は不可)
- 日付をまたいだセッションは 23:59:59 で一度フラッシュし、翌日 00:00 から再開する (集計を綺麗に保つため)

## バージョン

- v0.2: 単一 exe 配布対応 / 二重起動防止 / トレイツールチップ / シアンテーマ
- v0.1: 初版 (内部のみ)

## ライセンス

社内ツール。
