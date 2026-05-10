using System.Collections.ObjectModel;
using WorkTime.Models;
using WorkTime.ViewModels;

namespace WorkTime.Views;

/// <summary>
/// 設定ダイアログ用 ViewModel。AppConfig をそのまま編集する。
/// </summary>
public class SettingsViewModel : ObservableObject
{
    public AppConfig Config { get; }

    public ObservableCollection<TrackedProcess> Processes { get; }

    public RelayCommand AddProcessCommand { get; }
    public RelayCommand RemoveProcessCommand { get; }

    public SettingsViewModel(AppConfig config)
    {
        Config = config;
        Processes = new ObservableCollection<TrackedProcess>(config.TrackedProcesses);
        AddProcessCommand = new RelayCommand(_ =>
        {
            Processes.Add(new TrackedProcess { ProcessName = "", DisplayName = "", Enabled = true });
        });
        RemoveProcessCommand = new RelayCommand(p =>
        {
            if (p is TrackedProcess t) Processes.Remove(t);
        });
    }

    /// <summary>
    /// OK 押下時に Processes を Config に反映。
    /// </summary>
    public void Commit()
    {
        Config.TrackedProcesses.Clear();
        foreach (var p in Processes)
        {
            if (string.IsNullOrWhiteSpace(p.ProcessName)) continue;
            Config.TrackedProcesses.Add(p);
        }
    }
}
