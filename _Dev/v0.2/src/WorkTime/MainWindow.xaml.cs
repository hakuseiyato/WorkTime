using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WorkTime.Models;
using WorkTime.Services;
using WorkTime.ViewModels;
using WorkTime.Views;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using WinForms = System.Windows.Forms;

namespace WorkTime;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    // コンパクト ⇔ 通常切替用に通常時サイズを保持
    private double _normalWidth = 640;
    private double _normalHeight = 720;

    private const double CompactWidth = 460;
    private const double CompactHeight = 290;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Loaded += (_, _) => ApplyPlacement();
        ViewModel.CompactModeChanged += OnCompactModeChanged;
    }

    /// <summary>
    /// 設定から位置/サイズを復元する。Compact / Topmost も適用。
    /// </summary>
    private void ApplyPlacement()
    {
        var c = ViewModel.Config;

        // 通常時のサイズを保持 (config からの初期復元に利用)
        _normalWidth = c.WindowWidth > 100 ? c.WindowWidth : 640;
        _normalHeight = c.WindowHeight > 100 ? c.WindowHeight : 720;

        if (c.CompactMode)
        {
            Width = CompactWidth;
            Height = CompactHeight;
        }
        else
        {
            Width = _normalWidth;
            Height = _normalHeight;
        }

        if (!double.IsNaN(c.WindowLeft) && !double.IsNaN(c.WindowTop) && IsOnAnyScreen(c.WindowLeft, c.WindowTop))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = c.WindowLeft;
            Top = c.WindowTop;
        }

        if (string.Equals(c.WindowStateName, "Maximized", StringComparison.OrdinalIgnoreCase) && !c.CompactMode)
            WindowState = WindowState.Maximized;

        Topmost = c.AlwaysOnTop;
        AdjustRowHeights(c.CompactMode);
    }

    private static bool IsOnAnyScreen(double x, double y)
    {
        try
        {
            // ウィンドウ左上付近 (50,50) がいずれかのモニター範囲内に含まれるか
            int px = (int)Math.Floor(x) + 50;
            int py = (int)Math.Floor(y) + 50;
            foreach (var s in WinForms.Screen.AllScreens)
            {
                if (s.Bounds.Contains(px, py)) return true;
            }
        }
        catch { /* マルチモニター API 失敗時はフォールバック */ }
        return false;
    }

    private void OnCompactModeChanged(bool compact)
    {
        if (compact)
        {
            if (WindowState == WindowState.Normal)
            {
                _normalWidth = ActualWidth;
                _normalHeight = ActualHeight;
            }
            WindowState = WindowState.Normal;
            Width = CompactWidth;
            Height = CompactHeight;
        }
        else
        {
            Width = _normalWidth;
            Height = _normalHeight;
        }
        AdjustRowHeights(compact);
    }

    /// <summary>
    /// コンパクト時にプロジェクト一覧の * 行を 0 にして余白を消す。
    /// </summary>
    private void AdjustRowHeights(bool compact)
    {
        if (Content is not System.Windows.Controls.Grid grid) return;
        if (grid.RowDefinitions.Count < 6) return;
        grid.RowDefinitions[5].Height = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
    }

    private void SavePlacement()
    {
        var c = ViewModel.Config;
        if (WindowState == WindowState.Maximized)
        {
            c.WindowLeft = RestoreBounds.Left;
            c.WindowTop = RestoreBounds.Top;
            c.WindowWidth = RestoreBounds.Width > 100 ? RestoreBounds.Width : _normalWidth;
            c.WindowHeight = RestoreBounds.Height > 100 ? RestoreBounds.Height : _normalHeight;
            c.WindowStateName = "Maximized";
        }
        else if (c.CompactMode)
        {
            // コンパクト中は位置だけ保存。通常サイズは _normalWidth/Height を保持
            c.WindowLeft = Left;
            c.WindowTop = Top;
            c.WindowWidth = _normalWidth;
            c.WindowHeight = _normalHeight;
            c.WindowStateName = "Normal";
        }
        else
        {
            c.WindowLeft = Left;
            c.WindowTop = Top;
            c.WindowWidth = ActualWidth;
            c.WindowHeight = ActualHeight;
            c.WindowStateName = "Normal";
        }
        ConfigStore.Save(c);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        if (ViewModel?.Config?.MinimizeToTrayOnClose == true && App.Tray != null && !App.IsExiting)
        {
            // 閉じるボタン → トレイ最小化
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow
        {
            Owner = this,
            DataContext = new SettingsViewModel(ViewModel.Config)
        };
        if (dlg.ShowDialog() == true)
        {
            ConfigStore.Save(ViewModel.Config);
            // 自動起動レジストリを反映
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
                StartupRegistrar.SetEnabled(ViewModel.Config.LaunchAtStartup, exe);
            ViewModel.ReloadConfig();
        }
    }

    private void OnOpenCsv(object sender, RoutedEventArgs e)
    {
        var path = ViewModel.Logger.GetLogPath(DateTime.Now);
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "今月のログはまだありません。", "WorkTime", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"開けませんでした: {ex.Message}", "WorkTime", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
