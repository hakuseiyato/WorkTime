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
    /// <summary>設定画面で編集する監視フォルダ一覧。</summary>
    public ObservableCollection<TrackedFolder> Folders { get; }

    public RelayCommand AddProcessCommand { get; }
    public RelayCommand RemoveProcessCommand { get; }
    public RelayCommand AddFolderCommand { get; }
    public RelayCommand RemoveFolderCommand { get; }

    public SettingsViewModel(AppConfig config)
    {
        Config = config;
        Processes = new ObservableCollection<TrackedProcess>(config.TrackedProcesses);
        Folders = new ObservableCollection<TrackedFolder>(config.TrackedFolders);
        AddProcessCommand = new RelayCommand(_ =>
        {
            Processes.Add(new TrackedProcess { ProcessName = "", DisplayName = "", Enabled = true });
        });
        RemoveProcessCommand = new RelayCommand(p =>
        {
            if (p is TrackedProcess t) Processes.Remove(t);
        });
        AddFolderCommand = new RelayCommand(_ =>
        {
            Folders.Add(new TrackedFolder { Path = "", DisplayName = "", Enabled = true });
        });
        RemoveFolderCommand = new RelayCommand(p =>
        {
            if (p is TrackedFolder t) Folders.Remove(t);
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
        Config.TrackedFolders.Clear();
        foreach (var f in Folders)
        {
            if (string.IsNullOrWhiteSpace(f.Path)) continue;
            Config.TrackedFolders.Add(f);
        }
    }
}
