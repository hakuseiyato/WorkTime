using System;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// 計測中状態と現在セッションを保持する。Tick メソッドを定期的に呼ぶ前提。
/// </summary>
public class TimeTracker
{
    private readonly CsvLogger _logger;

    private DateTime? _sessionStart;
    private string _projectKey = "";
    private string _processName = "";
    private string _source = "manual";
    private DateTime _lastFlushBoundary;

    public bool IsRunning => _sessionStart.HasValue;

    /// <summary>
    /// 現在セッションの開始時刻 (running でないなら null)。
    /// </summary>
    public DateTime? SessionStart => _sessionStart;

    public string CurrentProjectKey => _projectKey;
    public string CurrentSource => _source;

    public event Action? SessionChanged;

    public TimeTracker(CsvLogger logger)
    {
        _logger = logger;
        _lastFlushBoundary = DateTime.Now.Date;
    }

    /// <summary>
    /// 計測を開始する。既に running なら projectKey が違うときのみ切り替え (前セッションをフラッシュ)。
    /// </summary>
    public void Start(string projectKey, string processName, string source)
    {
        if (IsRunning)
        {
            if (_projectKey == projectKey && _source == source) return;
            Stop();
        }
        _sessionStart = DateTime.Now;
        _projectKey = projectKey;
        _processName = processName;
        _source = source;
        SessionChanged?.Invoke();
    }

    /// <summary>
    /// 計測を停止し、現セッションを CSV へフラッシュする。
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;
        var rec = new SessionRecord
        {
            StartTime = _sessionStart!.Value,
            EndTime = DateTime.Now,
            ProjectKey = _projectKey,
            ProcessName = _processName,
            Source = _source
        };
        _logger.Append(rec);
        _sessionStart = null;
        _projectKey = "";
        _processName = "";
        SessionChanged?.Invoke();
    }

    /// <summary>
    /// 日付をまたいだら、その境界で一度フラッシュして翌日の 00:00 から再開する。
    /// </summary>
    public void HandleDayRollover()
    {
        if (!IsRunning) return;
        var now = DateTime.Now;
        if (now.Date == _sessionStart!.Value.Date) return;

        // 旧日: start - 23:59:59
        var endOfDay = _sessionStart.Value.Date.AddDays(1).AddSeconds(-1);
        var rec = new SessionRecord
        {
            StartTime = _sessionStart.Value,
            EndTime = endOfDay,
            ProjectKey = _projectKey,
            ProcessName = _processName,
            Source = _source
        };
        _logger.Append(rec);
        _sessionStart = now.Date; // 翌日の 00:00 から
    }

    /// <summary>
    /// 現セッションの累積を返す (running でなければ Zero)。
    /// </summary>
    public TimeSpan GetCurrentSpan()
    {
        if (!IsRunning) return TimeSpan.Zero;
        return DateTime.Now - _sessionStart!.Value;
    }
}
