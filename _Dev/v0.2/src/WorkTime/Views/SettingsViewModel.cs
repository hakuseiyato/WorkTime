using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WorkTime.Models;
using WorkTime.Services;
using WorkTime.ViewModels;

namespace WorkTime.Views;

/// <summary>
/// 設定ダイアログ用 ViewModel。録画設定は作業用コピーで編集する。
/// </summary>
public class SettingsViewModel : ObservableObject
{
    public AppConfig Config { get; }
    /// <summary>設定画面で編集する録画設定。</summary>
    public RecordingConfig Recording { get; }
    public string[] Encoders { get; } = new[] { "libx264", "h264_nvenc" };

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
        Recording = new RecordingConfig
        {
            Enabled = config.Recording.Enabled,
            OutputRoot = config.Recording.OutputRoot,
            Fps = config.Recording.Fps,
            LongEdge = config.Recording.LongEdge,
            Crf = config.Recording.Crf,
            Encoder = config.Recording.Encoder,
            PauseOnIdle = config.Recording.PauseOnIdle,
            AutoClipOnSessionEnd = config.Recording.AutoClipOnSessionEnd,
            ClipTargetSeconds = config.Recording.ClipTargetSeconds,
            FfmpegPath = config.Recording.FfmpegPath
        };
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
    /// 「起動中のアプリから選択」の結果を Processes に反映する。
    /// 紐づけ先はアプリ本体 (拡張子なしのプロセス名) であって、開いているファイルではない。
    /// 既に登録済みのものは表示名を上書きしない (ユーザーが付けた名前を壊さないため)。
    /// </summary>
    public void MergePickedApps(IEnumerable<(string ProcessName, string DisplayName, bool IsSelected)> picked)
    {
        foreach (var app in picked)
        {
            var key = ProcessMonitor.NormalizeProcessName(app.ProcessName);
            if (string.IsNullOrEmpty(key)) continue;

            var existing = Processes.FirstOrDefault(
                p => string.Equals(ProcessMonitor.NormalizeProcessName(p.ProcessName), key,
                                   StringComparison.OrdinalIgnoreCase));

            if (app.IsSelected)
            {
                if (existing != null) continue; // 既存の表示名は触らない
                Processes.Add(new TrackedProcess
                {
                    ProcessName = key,
                    DisplayName = string.IsNullOrWhiteSpace(app.DisplayName) ? key : app.DisplayName,
                    Enabled = true
                });
            }
            else if (existing != null)
            {
                Processes.Remove(existing);
            }
        }
    }

    /// <summary>
    /// OK 押下時に監視対象と録画設定を Config に反映。
    /// </summary>
    public void Commit()
    {
        Config.Recording.Enabled = Recording.Enabled;
        Config.Recording.OutputRoot = Recording.OutputRoot;
        Config.Recording.Fps = Recording.Fps;
        Config.Recording.LongEdge = Recording.LongEdge;
        Config.Recording.Crf = Recording.Crf;
        Config.Recording.Encoder = Recording.Encoder;
        Config.Recording.PauseOnIdle = Recording.PauseOnIdle;
        Config.Recording.AutoClipOnSessionEnd = Recording.AutoClipOnSessionEnd;
        Config.Recording.ClipTargetSeconds = Recording.ClipTargetSeconds;
        Config.Recording.FfmpegPath = Recording.FfmpegPath;
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
