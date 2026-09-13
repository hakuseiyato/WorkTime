using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WorkTime.Models;
using WorkTime.Services;
using WorkTime.ViewModels;

namespace WorkTime.Views;

/// <summary>
/// 一覧に出す 1 行。チェック状態は監視対象かどうかを表す。
/// </summary>
public class RunningAppItem : ObservableObject
{
    public string ProcessName { get; init; } = "";
    public string DisplayName { get; init; } = "";

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

/// <summary>
/// 起動中のアプリ一覧から監視対象を選ぶダイアログ。
/// 選択はアプリ本体 (実行ファイル名) に紐づく。開いているファイルには紐づけない。
/// </summary>
public partial class AppPickerDialog : Window
{
    public ObservableCollection<RunningAppItem> Apps { get; } = new();

    /// <summary>OK で確定した選択結果。キャンセル時は null。</summary>
    public List<RunningAppItem>? Result { get; private set; }

    private readonly List<TrackedProcess> _current;

    public AppPickerDialog(IEnumerable<TrackedProcess> currentTargets)
    {
        _current = currentTargets.ToList();
        InitializeComponent();
        DataContext = this;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Reload();
    }

    /// <summary>
    /// 起動中アプリを読み直す。すでに監視対象のものはチェック済みで出す。
    /// </summary>
    private void Reload()
    {
        var tracked = _current
            .Select(t => ProcessMonitor.NormalizeProcessName(t.ProcessName))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Apps.Clear();
        foreach (var app in RunningAppScanner.Scan())
        {
            Apps.Add(new RunningAppItem
            {
                ProcessName = app.ProcessName,
                DisplayName = app.DisplayName,
                IsSelected = tracked.Contains(app.ProcessName)
            });
        }
    }

    private void OnRescan(object sender, RoutedEventArgs e) => Reload();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = Apps.ToList();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
