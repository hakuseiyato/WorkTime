using System;

namespace WorkTime.Models;

/// <summary>
/// プロジェクト別の集計結果。MainViewModel 上のリストバインドに利用。
/// </summary>
public class ProjectSummary
{
    public string ProjectKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public TimeSpan Total { get; set; }

    public string TotalString
    {
        get
        {
            var t = Total;
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
