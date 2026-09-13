# WorkTime の構成

WorkTime は、Windows 上で対象アプリや監視フォルダの利用を検知し、作業時間をローカルの CSV に記録するデスクトップアプリ。v0.5 では計測セッションと連動するタイムラプス録画を追加した。録画は既定 OFF とし、ffmpeg の起動失敗が計測・メモ・打刻に波及しない構成にしている。

## 技術スタックと依存関係

- C# / WPF、ターゲットは `net8.0-windows`、バージョンは `0.5.0`。
- WinForms のモニタ情報、フォルダ選択ダイアログ、通知領域アイコンを使用。
- `System.Management` 8.0.0 を使い、監視フォルダ検知用のプロセス情報を WMI から取得。
- Win32 API でアイドル時間とタイトルバーの表示を制御。
- 録画時のみ外部の ffmpeg / ffprobe を使用。NuGet パッケージとしては追加しない。

## 主要なファイル

ソースルートは `_Dev/v0.2/src/WorkTime/`。

| パス | 役割 |
| --- | --- |
| `App.xaml(.cs)` | アプリ起動、二重起動制御、通知領域、終了処理、自動起動登録の修復 |
| `MainWindow.xaml(.cs)` | メイン画面、設定画面の呼び出し、ウィンドウ状態の保存 |
| `ViewModels/MainViewModel.cs` | 表示状態、コマンド、計測と録画の同期 |
| `Models/AppConfig.cs` / `RecordingConfig.cs` | 永続化する設定と録画の既定値 |
| `Services/TimeTracker.cs` | 計測セッション、開始時刻の修正、日付境界の処理 |
| `Services/ProcessMonitor.cs` / `OpenFileMonitor.cs` | 監視対象の検知 |
| `Services/CsvLogger.cs` / `ConfigStore.cs` | CSV と JSON の読み書き |
| `Services/CaptureRegion.cs` | 対象モニタの矩形取得と幅・高さの偶数化 |
| `Services/ScreenRecorder.cs` | ffmpeg 録画、正常停止、フレーム数取得とリネーム |
| `Services/ClipMaker.cs` | 録画済みファイルから早回しクリップを生成 |
| `Views/` | 設定、エクスポート、開始時刻修正のダイアログ |
| `Resources/Theme.xaml` | 共通の配色とコントロールスタイル |

## データと録画の流れ

監視タイマーが対象を検知し、`TimeTracker` がセッションを開始・停止する。`MainViewModel` はセッション通知とアイドル判定に応じて録画を同期する。手動画面録画は独立したフラグで区別する。クリップ変換は `Task.Run` で実行し、終了処理では新しい変換を起動しない。

設定は exe 配下の `data/config.json`、セッションと打刻は `data/logs/` に保存する。録画は設定した出力先の日付フォルダに保存し、クリップは録画元の日付フォルダ内の `clips/` に出力する。録画データは計測 CSV から独立している。

## ビルド・実行方法

Windows と .NET 8 SDK が必要。ユーザーが次のコマンドでビルドする。

```powershell
dotnet build .\_Dev\v0.2\src\WorkTime\WorkTime.csproj
```

配布済みアプリはルートの `WorkTime.exe` で起動する。`--tray` を付けると通知領域へ最小化して起動する。今回の変更ではビルド・テスト・配布 exe の再生成を実行していないため、コンパイラによる 0 エラー・0 警告と録画の実機動作は確認が必要。
