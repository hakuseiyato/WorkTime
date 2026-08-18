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
- 監視フォルダ: 指定フォルダ配下のファイルを開いていると自動で計測開始
- セッションごとのメモ入力 (CSV の `Memo` 列に記録)
- 「⚑ 打刻」ボタンでの打刻マーカー記録 (`data/logs/markers-YYYY-MM.csv`、作業時間の集計には影響しない)
- 計測中の開始時刻をクリックで手動修正
- 月ごとの CSV ログ (`data/logs/YYYY-MM.csv`)
- OS 起動時の自動起動 (HKCU\Run)

## 設定

メインウィンドウ右上「設定」から:

- 監視対象プロセス: プロセス名 (拡張子なし)、表示名、有効フラグを編集
- 監視フォルダ: フォルダパス、表示名、有効フラグを編集 (「参照…」ボタンでフォルダ選択)
- アイドル閾値 (分): 0 で無効
- 今日 / 今週 / 今月 の目標時間
- 閉じたらトレイへ最小化
- OS 起動時に自動起動

監視フォルダは、その配下のファイルを開いていると自動で計測を開始する仕組み。
プロセス名では区別できない案件別の計測に使う。フォルダ一致はプロセス一致より優先される。

設定は `data/config.json`、ログは `data/logs/YYYY-MM.csv` に保存される。
どちらも `WorkTime.exe` と同じフォルダ直下に作成される。

## CSV フォーマット

`data/logs/YYYY-MM.csv`、UTF-8 ヘッダ付き:

```
Date,StartTime,EndTime,DurationSec,ProjectKey,ProcessName,Source,Memo
2026-05-10,09:30:12,11:42:53,7961,Unity,Unity,auto,シーン調整
2026-05-10,13:15:00,14:00:00,2700,Manual,Manual,manual,
2026-05-10,15:10:00,16:05:30,3330,Docs,notepad,auto,"README更新, 動作確認"
```

`Source` は `auto` (プロセス検知) または `manual` (手動トグル)。

v0.3 以前の 7 列ログ (`Memo` なし) もそのまま読み込める (後方互換)。

打刻マーカーは `data/logs/markers-YYYY-MM.csv` に UTF-8 ヘッダ付きで記録される:

```
Date,Time,ProjectKey,Memo
2026-05-10,10:15:30,Unity,レビュー開始
2026-05-10,12:05:00,(未計測),離席
```

打刻マーカーは作業時間の集計には一切影響しない。

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
   ├─ Models/                   AppConfig / SessionRecord / MarkerRecord / TrackedFolder / ProjectSummary
   ├─ Services/                 ProcessMonitor / OpenFileMonitor / IdleDetector / TimeTracker / CsvLogger /
   │                            ConfigStore / StartupRegistrar / SingleInstanceSignal / DarkTitleBar
   ├─ ViewModels/               MainViewModel / RelayCommand / ObservableObject
   ├─ Views/                    SettingsWindow / ExportDialog / TimeEditDialog & VM
   ├─ Controls/                 FlipCard
   ├─ Resources/Theme.xaml      ダークテーマ (ArtNet Manager 風シアン/ティール)
   ├─ App.xaml(.cs)             エントリ + タスクトレイ + 二重起動防止
   └─ MainWindow.xaml(.cs)
```

## 既知の制限

- プロセス名一致だけでは同名プロセスを区別できない (例: Unity のプロジェクト別追跡)。案件別に分けたい場合は監視フォルダを使う
- 日付をまたいだセッションは 23:59:59 で一度フラッシュし、翌日 00:00 から再開する (集計を綺麗に保つため)
- 監視フォルダの検知はプロセスのコマンドラインとウィンドウタイトルに依存する。アプリ内の File > Open で開いたファイルは、ウィンドウタイトルにパスやフォルダ名が出ないアプリでは検知できない
- 監視フォルダの検知には WMI を使うため、負荷対策として 10 秒間キャッシュされる (最大 10 秒程度の検知遅延がある)
- ウィンドウタイトルの「フォルダ名だけ」での一致は、監視対象プロセスに登録したアプリのウィンドウに限定している。
  エクスプローラやターミナルでフォルダを開いているだけでは計測は始まらない (フルパスがコマンドラインやタイトルに出ている場合はどのアプリでも検知する)
- フォルダ名が 2 文字以下の場合は誤検知回避のため、フォルダ名だけの一致判定は行わない

## 配布と引継ぎ

### 配布パッケージの作成

```powershell
.\package.ps1
# 出力: WorkTime_v<version>.zip
```

zip には `WorkTime.exe` + `README.txt` + 空の `data/` が入っています。
受け取った人は zip を解凍して `WorkTime.exe` をダブルクリックするだけ。
`-SkipPublish` で既存 exe を再利用、`-Open` で完了後にエクスプローラを開きます。

### データ引継ぎ

旧環境の `data/` フォルダ（`config.json` と `logs/YYYY-MM.csv`）を、
新環境の `WorkTime.exe` と同じ階層にコピーするだけで、設定もログも丸ごと復元できます。

```
旧 PC:  ...\WorkTime\data\           ← まるごとコピー
新 PC:  ...\WorkTime\data\           ← ここに上書き貼り付け
```

WorkTime を起動中の場合は一度終了してから入れ替えてください。

## 集計エクスポート

メインウィンドウ右下「集計エクスポート」から:

- 期間プリセット (今日/今週/今月/先月/全期間) または手動 DatePicker
- プロジェクト別合計を一覧で表示、チェックボックスで選択
- 「CSV エクスポート」で UTF-8 BOM 付き CSV を保存
- 出力にはサマリ（期間/合計/セッション数/プロジェクト別小計）+ 詳細セッションが含まれる

## バージョン

- v0.4: セッションメモ + 打刻マーカー + 監視フォルダ自動検知 + 開始時刻の手動修正
- v0.3: カスタムタイトルバー + コンパクト + 最前面固定 + 集計エクスポート
- v0.2: 単一 exe 配布対応 / 二重起動防止 / トレイツールチップ / シアンテーマ
- v0.1: 初版 (内部のみ)

## ライセンス

社内ツール。
