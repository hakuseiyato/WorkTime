using System;
using WorkTime.ViewModels;

namespace WorkTime.Models;

/// <summary>
/// プロジェクト別の集計結果。MainViewModel 上のリストバインドに利用。
/// IsSelected はチェックボックス連動でエクスポート対象を切り替える。
/// </summary>
public class ProjectSummary : ObservableObject
{
    public string ProjectKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public TimeSpan Total { get; set; }

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
