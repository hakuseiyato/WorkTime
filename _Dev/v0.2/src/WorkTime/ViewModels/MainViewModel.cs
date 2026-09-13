using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using WorkTime.Models;
using WorkTime.Services;

namespace WorkTime.ViewModels;

/// <summary>
/// メイン画面の状態と更新ロジックを担う。
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _monitorTimer;

    /// <summary>
    /// ユーザーが「停止」を押した直後の対象プロジェクトキー。
    /// 同じ対象が検知され続けている間は自動再開を抑止する。
    /// 別対象に切り替わる、または対象が検知されなくなったら null に戻す。
    /// </summary>
    private string? _pausedProjectKey;

    /// <summary>打刻フィードバックを StatusDetail に出しておく期限。</summary>
    private DateTime _markFeedbackUntil = DateTime.MinValue;

    public AppConfig Config { get; private set; }
    public ProcessMonitor Monitor { get; }
    /// <summary>監視対象フォルダ配下のファイルを開いているかを判定する。</summary>
    public OpenFileMonitor FolderMonitor { get; }
    public TimeTracker Tracker { get; }
    public CsvLogger Logger { get; }

    // ===== 表示用プロパティ =====

    private string _hours = "00";
    public string Hours { get => _hours; set => SetProperty(ref _hours, value); }

    private string _minutes = "00";
    public string Minutes { get => _minutes; set => SetProperty(ref _minutes, value); }

    private string _seconds = "00";
    public string Seconds { get => _seconds; set => SetProperty(ref _seconds, value); }

    private string _statusText = "停止中";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private string _statusDetail = "";
    public string StatusDetail { get => _statusDetail; set => SetProperty(ref _statusDetail, value); }

    /// <summary>"running" / "paused" / "stopped" — XAML 側で DataTrigger で配色を切り替える。</summary>
    private string _statusKind = "stopped";
    public string StatusKind { get => _statusKind; set => SetProperty(ref _statusKind, value); }

    public bool HasNoProjects => ProjectSummaries.Count == 0;

    private void OnProjectSummaryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProjectSummary.IsSelected)) return;
        if (sender is not ProjectSummary ps) return;
        Config.UnselectedProjects ??= new();
        var list = Config.UnselectedProjects;
        if (ps.IsSelected)
        {
            list.RemoveAll(s => string.Equals(s, ps.ProjectKey, StringComparison.OrdinalIgnoreCase));
        }
        else if (!list.Any(s => string.Equals(s, ps.ProjectKey, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(ps.ProjectKey);
        }
        ConfigStore.Save(Config);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(ToggleButtonLabel));
            }
        }
    }

    public string ToggleButtonLabel => IsRunning ? "■ 停止" : "▶ 開始";

    private string _scope = "today";
    public string Scope
    {
        get => _scope;
        set
        {
            if (SetProperty(ref _scope, value))
            {
                OnPropertyChanged(nameof(IsScopeToday));
                OnPropertyChanged(nameof(IsScopeWeek));
                OnPropertyChanged(nameof(IsScopeMonth));
                OnPropertyChanged(nameof(IsScopeAll));
                Refresh();
            }
        }
    }

    public bool IsScopeToday => Scope == "today";
    public bool IsScopeWeek => Scope == "week";
    public bool IsScopeMonth => Scope == "month";
    public bool IsScopeAll => Scope == "all";

    private double _progressPercent;
    public double ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }

    private string _progressLabel = "";
    public string ProgressLabel { get => _progressLabel; set => SetProperty(ref _progressLabel, value); }

    public ObservableCollection<ProjectSummary> ProjectSummaries { get; } = new();

    private bool _autoDetectEnabled;
    public bool AutoDetectEnabled
    {
        get => _autoDetectEnabled;
        set
        {
            if (SetProperty(ref _autoDetectEnabled, value))
            {
                Config.AutoDetectEnabled = value;
                ConfigStore.Save(Config);
                // 即時反映: トグルした瞬間に検知/停止が効くようにする
                OnMonitorTick();
            }
        }
    }

    private bool _alwaysOnTop;
    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (SetProperty(ref _alwaysOnTop, value))
            {
                Config.AlwaysOnTop = value;
                ConfigStore.Save(Config);
                AlwaysOnTopChanged?.Invoke(value);
            }
        }
    }

    private bool _compactMode;
    public bool CompactMode
    {
        get => _compactMode;
        set
        {
            if (SetProperty(ref _compactMode, value))
            {
                Config.CompactMode = value;
                ConfigStore.Save(Config);
                CompactModeChanged?.Invoke(value);
            }
        }
    }

    /// <summary>CompactMode が変化したときにウィンドウ側がリサイズできるよう通知。</summary>
    public event Action<bool>? CompactModeChanged;

    /// <summary>
    /// AlwaysOnTop が変化したときにウィンドウ側が Topmost を追随させるよう通知。
    /// XAML バインディングにしないのは、前面化処理で Topmost へ直接代入した瞬間に
    /// ローカル値がバインディングを上書きして以降効かなくなるため (v0.3.4 の再発防止)。
    /// </summary>
    public event Action<bool>? AlwaysOnTopChanged;

    // ===== コマンド =====

    /// <summary>現在セッションのメモ。計測中いつでも書き換えられる。</summary>
    public string Memo
    {
        get => Tracker.CurrentMemo;
        set { Tracker.CurrentMemo = value; OnPropertyChanged(); }
    }

    /// <summary>計測中の開始打刻表示。未計測なら空文字 (UI 側で非表示になる)。</summary>
    public string SessionStartText =>
        Tracker.IsRunning ? $"開始 {Tracker.SessionStart!.Value:HH:mm:ss}" : "";

    public RelayCommand ToggleCommand { get; }
    public RelayCommand SetScopeCommand { get; }
    public RelayCommand MarkCommand { get; }

    public MainViewModel()
    {
        Config = ConfigStore.Load();
        _autoDetectEnabled = Config.AutoDetectEnabled;
        _alwaysOnTop = Config.AlwaysOnTop;
        _compactMode = Config.CompactMode;
        _scope = string.IsNullOrWhiteSpace(Config.TargetScope) ? "today" : Config.TargetScope;

        Logger = new CsvLogger();
        Tracker = new TimeTracker(Logger);
        Tracker.SessionChanged += () =>
        {
            IsRunning = Tracker.IsRunning;
            OnPropertyChanged(nameof(Memo));           // Stop でクリアされた分を UI に反映
            OnPropertyChanged(nameof(SessionStartText));
            UpdateStatus();
        };

        Monitor = new ProcessMonitor { Targets = Config.TrackedProcesses };
        FolderMonitor = new OpenFileMonitor
        {
            Targets = Config.TrackedFolders,
            KnownApps = Config.TrackedProcesses
        };

        ToggleCommand = new RelayCommand(OnToggle);
        SetScopeCommand = new RelayCommand(p => { if (p is string s) Scope = s; });
        MarkCommand = new RelayCommand(OnMark);

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _tickTimer.Tick += (_, _) => OnTick();
        _tickTimer.Start();

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _monitorTimer.Tick += (_, _) => OnMonitorTick();
        _monitorTimer.Start();

        // 起動直後に 1 度だけプロセス検知
        OnMonitorTick();
        Refresh();
    }

    /// <summary>
    /// 設定を再読み込みし、状態を反映する。
    /// </summary>
    public void ReloadConfig()
    {
        Config = ConfigStore.Load();
        Monitor.Targets = Config.TrackedProcesses;
        FolderMonitor.Targets = Config.TrackedFolders;
        FolderMonitor.KnownApps = Config.TrackedProcesses;
        AutoDetectEnabled = Config.AutoDetectEnabled;
        Refresh();
    }

    // ===== コマンド実装 =====

    private void OnToggle(object? _)
    {
        if (Tracker.IsRunning)
        {
            // 自動セッションを手動停止した場合、同じ対象が起動している限り再開させない
            bool wasAuto = Tracker.CurrentSource == "auto";
            string pausedKey = Tracker.CurrentProjectKey;
            Tracker.Stop();
            _pausedProjectKey = (wasAuto && AutoDetectEnabled && !string.IsNullOrEmpty(pausedKey))
                ? pausedKey
                : null;
        }
        else
        {
            // 手動 Start: 一時停止を解除
            _pausedProjectKey = null;
            Tracker.Start("Manual", "Manual", "manual");
        }
        UpdateStatus();
        Refresh();
    }

    /// <summary>
    /// 現在時刻に打刻マーカーを 1 件残す。作業時間の集計には影響しない。
    /// メモ欄は打刻後もクリアしない (連続した打刻に使い回せるようにするため)。
    /// </summary>
    private void OnMark(object? _)
    {
        var now = DateTime.Now;
        var key = Tracker.IsRunning && !string.IsNullOrWhiteSpace(Tracker.CurrentProjectKey)
            ? Tracker.CurrentProjectKey
            : "(未計測)";
        Logger.AppendMarker(new MarkerRecord
        {
            Timestamp = now,
            ProjectKey = key,
            Memo = Memo
        });
        // 打刻した手応えを 3 秒だけ出す (UpdateStatus は 0.5 秒ごとに走るため期限で制御する)
        _markFeedbackUntil = now.AddSeconds(3);
        StatusDetail = $"⚑ {now:HH:mm:ss} 打刻";
    }

    // ===== タイマー Tick =====

    private void OnTick()
    {
        Tracker.HandleDayRollover();
        UpdateClock();
        UpdateStatus();
        OnPropertyChanged(nameof(SessionStartText));
    }

    private void OnMonitorTick()
    {
        if (!AutoDetectEnabled)
        {
            // オートが無効でも、手動セッションのアイドル一時停止だけは行う
            CheckIdle(forceManualStop: true);
            return;
        }

        // フォルダ一致を優先して評価する。当たらなければ従来のプロセス一致にフォールバック。
        string? hitKey = null;
        string hitProcessName = "";

        var folderHit = FolderMonitor.FindOpenTarget();
        if (folderHit != null)
        {
            var leaf = GetFolderLeafName(folderHit.Path);
            hitKey = string.IsNullOrWhiteSpace(folderHit.DisplayName) ? leaf : folderHit.DisplayName;
            hitProcessName = "";
        }
        else
        {
            var processHit = Monitor.FindRunningTarget();
            if (processHit != null)
            {
                hitKey = string.IsNullOrWhiteSpace(processHit.DisplayName)
                    ? processHit.ProcessName
                    : processHit.DisplayName;
                hitProcessName = processHit.ProcessName;
            }
        }
        var idleMin = Math.Max(0, Config.IdleThresholdMinutes);
        var idle = IdleDetector.GetIdleTime();
        bool isIdle = idleMin > 0 && idle >= TimeSpan.FromMinutes(idleMin);

        // 手動停止スナップショット: 同一対象が検知され続けている間は静観
        if (_pausedProjectKey != null)
        {
            if (hitKey == null ||
                !string.Equals(hitKey, _pausedProjectKey, StringComparison.OrdinalIgnoreCase))
            {
                // 対象が消えた / 別対象に切り替わった → 一時停止解除
                _pausedProjectKey = null;
            }
            else
            {
                return;
            }
        }

        if (hitKey != null && !isIdle)
        {
            if (!Tracker.IsRunning || Tracker.CurrentSource == "manual")
            {
                // 手動中なら手動を優先 (停止しない)
                if (Tracker.IsRunning && Tracker.CurrentSource == "manual") return;
                Tracker.Start(hitKey, hitProcessName, "auto");
            }
            else if (Tracker.CurrentProjectKey != hitKey)
            {
                Tracker.Start(hitKey, hitProcessName, "auto");
            }
        }
        else
        {
            // 自動セッション中なら停止。手動中は触らない。
            if (Tracker.IsRunning && Tracker.CurrentSource == "auto")
                Tracker.Stop();
        }

        Refresh();
    }

    /// <summary>監視フォルダのパスから末尾のフォルダ名を取り出す (表示名が空のときのキーに使う)。</summary>
    private static string GetFolderLeafName(string path)
    {
        try
        {
            var full = System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');
            var leaf = System.IO.Path.GetFileName(full);
            return string.IsNullOrEmpty(leaf) ? full : leaf;
        }
        catch
        {
            return path;
        }
    }

    private void CheckIdle(bool forceManualStop)
    {
        var idleMin = Config.IdleThresholdMinutes;
        if (idleMin <= 0) return;
        var idle = IdleDetector.GetIdleTime();
        if (idle < TimeSpan.FromMinutes(idleMin)) return;

        if (Tracker.IsRunning && Tracker.CurrentSource == "auto")
            Tracker.Stop();
        // 手動セッションは閾値を超えても停止しない (ユーザー意思を優先)
    }

    // ===== 表示更新 =====

    private DateTime _lastRefresh = DateTime.MinValue;

    /// <summary>
    /// スコープ集計とプロジェクトリストを再計算。
    /// </summary>
    public void Refresh()
    {
        _lastRefresh = DateTime.Now;
        var (from, to, label, target) = ResolveScope();

        var records = Logger.Load(from, to);
        TimeSpan total = TimeSpan.Zero;
        var perProject = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            var s = r.StartTime < from ? from : r.StartTime;
            var e = r.EndTime > to ? to : r.EndTime;
            if (e <= s) continue;
            var span = e - s;
            total += span;
            var k = string.IsNullOrWhiteSpace(r.ProjectKey) ? "(その他)" : r.ProjectKey;
            perProject[k] = perProject.TryGetValue(k, out var v) ? v + span : span;
        }
        if (Tracker.IsRunning)
        {
            var s = Tracker.SessionStart!.Value;
            var e = DateTime.Now;
            if (s < from) s = from;
            if (e > to) e = to;
            if (e > s)
            {
                var span = e - s;
                total += span;
                var k = string.IsNullOrWhiteSpace(Tracker.CurrentProjectKey) ? "(その他)" : Tracker.CurrentProjectKey;
                perProject[k] = perProject.TryGetValue(k, out var v) ? v + span : span;
            }
        }

        UpdateClockFromSpan(total);

        // 既存の購読を解除して Clear
        foreach (var ps in ProjectSummaries)
            ps.PropertyChanged -= OnProjectSummaryPropertyChanged;
        ProjectSummaries.Clear();

        var unselected = new HashSet<string>(Config.UnselectedProjects ?? new(), StringComparer.OrdinalIgnoreCase);
        foreach (var kv in perProject.OrderByDescending(p => p.Value))
        {
            var ps = new ProjectSummary
            {
                ProjectKey = kv.Key,
                DisplayName = kv.Key,
                Total = kv.Value,
                IsSelected = !unselected.Contains(kv.Key)
            };
            ps.PropertyChanged += OnProjectSummaryPropertyChanged;
            ProjectSummaries.Add(ps);
        }
        OnPropertyChanged(nameof(HasNoProjects));

        if (target.TotalSeconds > 0)
        {
            ProgressPercent = Math.Min(100.0, total.TotalSeconds / target.TotalSeconds * 100.0);
            ProgressLabel = $"{label} 目標 {FormatHm(target)} / 達成 {FormatHm(total)} ({ProgressPercent:F1}%)";
        }
        else
        {
            ProgressPercent = 0;
            ProgressLabel = $"{label} 累計 {FormatHm(total)}";
        }

        Config.TargetScope = Scope;
        ConfigStore.Save(Config);
    }

    private (DateTime from, DateTime to, string label, TimeSpan target) ResolveScope()
    {
        var now = DateTime.Now;
        switch (Scope)
        {
            case "week":
            {
                int dow = (int)now.DayOfWeek; // Sun=0
                int offset = (dow == 0) ? 6 : dow - 1; // 月曜起点
                var from = now.Date.AddDays(-offset);
                return (from, from.AddDays(7), "今週", TimeSpan.FromHours(Config.WeeklyTargetHours));
            }
            case "month":
            {
                var from = new DateTime(now.Year, now.Month, 1);
                return (from, from.AddMonths(1), "今月", TimeSpan.FromHours(Config.MonthlyTargetHours));
            }
            case "all":
            {
                var from = new DateTime(2000, 1, 1);
                var to = now.Date.AddDays(1);
                return (from, to, "全期間", TimeSpan.Zero);
            }
            default:
            {
                var from = now.Date;
                return (from, from.AddDays(1), "今日", TimeSpan.FromHours(Config.DailyTargetHours));
            }
        }
    }

    private void UpdateClock()
    {
        // 現在進行中セッションを反映するため、毎 tick で集計をやり直すのは重いので
        // Scope=today のときだけ、現在セッション分を加算したライブ表示を行う。
        if (Scope != "today")
        {
            // それ以外のスコープは Refresh が呼ばれたときに更新する
            return;
        }
        // ここで軽量な再計算: 最後の Refresh から +1秒ごとの差分を加算するのが理想だが、
        // シンプルさを優先して毎 0.5 秒 Refresh を回す。負荷が出るならキャッシュ化する。
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds > 500)
            Refresh();
    }

    private void UpdateClockFromSpan(TimeSpan total)
    {
        var h = (int)total.TotalHours;
        Hours = h.ToString("D2");
        Minutes = total.Minutes.ToString("D2");
        Seconds = total.Seconds.ToString("D2");
    }

    private void UpdateStatus()
    {
        // 打刻直後の 3 秒だけ StatusDetail を守る (StatusText/StatusKind は通常どおり更新する)
        bool keepDetail = DateTime.Now < _markFeedbackUntil;
        void SetDetail(string s) { if (!keepDetail) StatusDetail = s; }

        if (Tracker.IsRunning)
        {
            StatusText = Tracker.CurrentSource == "auto" ? "● 自動計測中" : "● 手動計測中";
            SetDetail(Tracker.CurrentProjectKey);
            StatusKind = "running";
        }
        else if (_pausedProjectKey != null)
        {
            StatusText = "⏸ 一時停止中";
            SetDetail($"{_pausedProjectKey} 検知中 — 再開するには「開始」を押下");
            StatusKind = "paused";
        }
        else
        {
            StatusText = "○ 停止中";
            SetDetail(AutoDetectEnabled ? "対象アプリ起動を待機中" : "手動モード");
            StatusKind = "stopped";
        }
    }

    private static string FormatHm(TimeSpan t)
    {
        return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
    }

    /// <summary>
    /// アプリ終了時に呼び出す。進行中セッションをフラッシュ。
    /// </summary>
    public void Shutdown()
    {
        _tickTimer.Stop();
        _monitorTimer.Stop();
        Tracker.Stop();
    }
}
