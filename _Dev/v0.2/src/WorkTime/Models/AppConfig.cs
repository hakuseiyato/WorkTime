using System.Collections.Generic;

namespace WorkTime.Models;

/// <summary>
/// アプリ全体の設定情報。data/config.json に永続化される。
/// </summary>
public class AppConfig
{
    /// <summary>監視対象プロセス。拡張子なしのプロセス名 (例: "Unity", "blender")。</summary>
    public List<TrackedProcess> TrackedProcesses { get; set; } = new();

    /// <summary>無操作時に自動で計測停止するまでの分数。0 で無効。</summary>
    public int IdleThresholdMinutes { get; set; } = 5;

    /// <summary>目標時間 (時間単位)。進捗バー表示に利用。</summary>
    public double DailyTargetHours { get; set; } = 8.0;

    public double WeeklyTargetHours { get; set; } = 40.0;

    public double MonthlyTargetHours { get; set; } = 160.0;

    /// <summary>進捗バーの基準: today / week / month / all</summary>
    public string TargetScope { get; set; } = "today";

    /// <summary>true でテーマをダークに。false でライト。</summary>
    public bool DarkMode { get; set; } = true;

    /// <summary>true で自動検知 ON、false で手動モードのみ。</summary>
    public bool AutoDetectEnabled { get; set; } = true;

    /// <summary>ウィンドウクローズ時にトレイへ最小化するか。</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>OS 起動時に自動起動するか (HKCU\Run に登録)。</summary>
    public bool LaunchAtStartup { get; set; } = false;

    /// <summary>true で常にウィンドウを最前面に固定。</summary>
    public bool AlwaysOnTop { get; set; } = false;

    /// <summary>true でコンパクト表示 (時計と状態のみ)。</summary>
    public bool CompactMode { get; set; } = false;

    // ===== ウィンドウ位置/サイズの永続化 =====
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 640;
    public double WindowHeight { get; set; } = 720;
    /// <summary>"Normal" / "Maximized" — 最大化中に閉じた場合復元する用。</summary>
    public string WindowStateName { get; set; } = "Normal";

    /// <summary>
    /// チェックを外したプロジェクトキーの一覧 (大文字小文字無視)。
    /// 既定はすべて選択 (= リストに無い)。エクスポート対象除外に利用。
    /// </summary>
    public List<string> UnselectedProjects { get; set; } = new();
}

/// <summary>
/// 監視対象プロセスの定義。プロジェクト名は表示・集計用のキーとして利用。
/// </summary>
public class TrackedProcess
{
    /// <summary>プロセス名 (拡張子なし、大文字小文字無視)。例: "Unity", "AfterFX"</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>表示用の別名。空なら ProcessName を利用。</summary>
    public string DisplayName { get; set; } = "";

    public bool Enabled { get; set; } = true;
}
