using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WorkTime.Services;
using WorkTime.ViewModels;
using WorkTime.Views;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace WorkTime;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
