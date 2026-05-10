using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using WorkTime.Models;
using WorkTime.Services;
using WorkTime.ViewModels;

namespace WorkTime.Views;

/// <summary>
/// 集計エクスポート用 ViewModel。期間とプロジェクト選択を受けて CSV を出力する。
/// </summary>
public class ExportViewModel : ObservableObject
{
    private readonly CsvLogger _logger;
    private readonly AppConfig _config;

    private DateTime _fromDate;
    public DateTime FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value)) RefreshProjects();
        }
    }

    private DateTime _toDate;
    public DateTime ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value)) RefreshProjects();
        }
    }

    public ObservableCollection<ExportProjectItem> Projects { get; } = new();

    private TimeSpan _selectedTotal;
    public TimeSpan SelectedTotal
    {
        get => _selectedTotal;
        private set
        {
            if (SetProperty(ref _selectedTotal, value))
                OnPropertyChanged(nameof(SelectedTotalString));
        }
    }

    public string SelectedTotalString
    {
        get
        {
            var t = _selectedTotal;
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }

    private int _selectedSessionCount;
    public int SelectedSessionCount
    {
        get => _selectedSessionCount;
        private set => SetProperty(ref _selectedSessionCount, value);
    }

    public ExportViewModel(CsvLogger logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
        var now = DateTime.Now;
        _fromDate = new DateTime(now.Year, now.Month, 1);
        _toDate = now.Date;
        RefreshProjects();
    }

    /// <summary>期間プリセット適用。</summary>
    public void ApplyPreset(string key)
    {
        var now = DateTime.Now;
        switch (key)
        {
            case "today":
                FromDate = now.Date; ToDate = now.Date; break;
            case "week":
            {
                int dow = (int)now.DayOfWeek;
                int offset = (dow == 0) ? 6 : dow - 1;
                FromDate = now.Date.AddDays(-offset);
                ToDate = now.Date;
                break;
            }
            case "month":
                FromDate = new DateTime(now.Year, now.Month, 1);
                ToDate = now.Date;
                break;
            case "lastMonth":
            {
                var first = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                FromDate = first;
                ToDate = first.AddMonths(1).AddDays(-1);
                break;
            }
            case "all":
                FromDate = new DateTime(2000, 1, 1);
                ToDate = now.Date;
                break;
        }
    }

    /// <summary>
    /// 期間内のセッションを読み、プロジェクト別合計を算出して Projects を更新する。
    /// </summary>
    public void RefreshProjects()
    {
        if (_toDate < _fromDate) return;

        // 既存のチェック状態を維持。初回は AppConfig.UnselectedProjects から導出。
        Dictionary<string, bool> prev;
        if (Projects.Count == 0)
        {
            var unset = new HashSet<string>(_config.UnselectedProjects ?? new(), StringComparer.OrdinalIgnoreCase);
            prev = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            // すべて初期 true。unselected に入っているキーだけ false。
            foreach (var key in unset) prev[key] = false;
        }
        else
        {
            prev = Projects.ToDictionary(p => p.ProjectKey, p => p.IsSelected, StringComparer.OrdinalIgnoreCase);
        }
        Projects.Clear();

        var to = _toDate.Date.AddDays(1);
        var records = _logger.Load(_fromDate.Date, to);
        var byProj = new Dictionary<string, (TimeSpan Total, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            var s = r.StartTime < _fromDate.Date ? _fromDate.Date : r.StartTime;
            var e = r.EndTime > to ? to : r.EndTime;
            if (e <= s) continue;
            var key = string.IsNullOrWhiteSpace(r.ProjectKey) ? "(その他)" : r.ProjectKey;
            byProj.TryGetValue(key, out var cur);
            byProj[key] = (cur.Total + (e - s), cur.Count + 1);
        }

        foreach (var kv in byProj.OrderByDescending(p => p.Value.Total))
        {
            var item = new ExportProjectItem
            {
                ProjectKey = kv.Key,
                Total = kv.Value.Total,
                SessionCount = kv.Value.Count,
                // 既存の選択状態を維持。新規プロジェクトは初期 ON。
                IsSelected = prev.TryGetValue(kv.Key, out var sel) ? sel : true
            };
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ExportProjectItem.IsSelected))
                    UpdateTotals();
            };
            Projects.Add(item);
        }
        UpdateTotals();
    }

    public void UpdateTotals()
    {
        TimeSpan total = TimeSpan.Zero;
        int count = 0;
        foreach (var p in Projects)
        {
            if (!p.IsSelected) continue;
            total += p.Total;
            count += p.SessionCount;
        }
        SelectedTotal = total;
        SelectedSessionCount = count;
    }

    public void SelectAll(bool on)
    {
        foreach (var p in Projects) p.IsSelected = on;
    }

    /// <summary>
    /// 選択プロジェクトのセッション + サマリを CSV へ書き出す。
    /// </summary>
    public void ExportTo(string path)
    {
        var to = _toDate.Date.AddDays(1);
        var records = _logger.Load(_fromDate.Date, to);
        var selected = Projects.Where(p => p.IsSelected)
                               .Select(p => p.ProjectKey)
                               .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // プロジェクト別合計
        var byProj = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var matched = new List<SessionRecord>();
        foreach (var r in records)
        {
            var key = string.IsNullOrWhiteSpace(r.ProjectKey) ? "(その他)" : r.ProjectKey;
            if (!selected.Contains(key)) continue;
            var s = r.StartTime < _fromDate.Date ? _fromDate.Date : r.StartTime;
            var e = r.EndTime > to ? to : r.EndTime;
            if (e <= s) continue;
            byProj.TryGetValue(key, out var cur);
            byProj[key] = cur + (e - s);
            matched.Add(r);
        }

        var sb = new StringBuilder();
        sb.AppendLine("# WorkTime 集計エクスポート");
        sb.AppendLine($"# 期間,{_fromDate:yyyy-MM-dd},{_toDate:yyyy-MM-dd}");
        sb.AppendLine($"# 合計,{FormatHms(byProj.Values.Aggregate(TimeSpan.Zero, (a, b) => a + b))}");
        sb.AppendLine($"# セッション数,{matched.Count}");
        sb.AppendLine();
        sb.AppendLine("# プロジェクト別合計");
        sb.AppendLine("ProjectKey,Total(hh:mm:ss),TotalSec");
        foreach (var kv in byProj.OrderByDescending(p => p.Value))
        {
            sb.AppendLine($"{Escape(kv.Key)},{FormatHms(kv.Value)},{(long)kv.Value.TotalSeconds}");
        }
        sb.AppendLine();
        sb.AppendLine("# 詳細セッション");
        sb.AppendLine(SessionRecord.CsvHeader);
        foreach (var r in matched.OrderBy(r => r.StartTime))
        {
            sb.AppendLine(r.ToCsvRow());
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // BOM 付きで Excel 互換
    }

    private static string FormatHms(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

/// <summary>
/// エクスポート対象プロジェクトの 1 行。
/// </summary>
public class ExportProjectItem : ObservableObject
{
    public string ProjectKey { get; set; } = "";
    public TimeSpan Total { get; set; }
    public int SessionCount { get; set; }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string TotalString
    {
        get
        {
            var t = Total;
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
