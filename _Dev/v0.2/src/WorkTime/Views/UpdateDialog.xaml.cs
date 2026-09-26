using System;
using System.Threading;
using System.Windows;
using WorkTime.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace WorkTime.Views;

/// <summary>
/// 更新が見つかったことを知らせ、適用するかをユーザーに決めてもらうダイアログ。
/// 自動では適用しない。作業中に勝手に再起動すると計測と録画が切れるため。
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _info;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>更新を適用して新しい exe を起動できた。呼び出し側はアプリを終了させること。</summary>
    public bool Applied { get; private set; }

    public UpdateDialog(UpdateInfo info)
    {
        _info = info;
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);

        CurrentText.Text = "v" + UpdateChecker.CurrentVersion;
        LatestText.Text = _info.Tag + (info.Size > 0 ? $"  ({info.Size / 1024 / 1024} MB)" : "");
        TitleText.Text = _info.Title;
    }

    private async void OnUpdate(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressArea.Visibility = Visibility.Visible;

        var progress = new Progress<double>(v => Bar.Value = Math.Clamp(v * 100.0, 0, 100));
        string failure = "";

        var ok = await SelfUpdater.ApplyAsync(_info, progress, r => failure = r, _cts.Token);

        if (ok)
        {
            Applied = true;
            DialogResult = true;
            Close();
            return;
        }

        ProgressArea.Visibility = Visibility.Collapsed;
        UpdateButton.IsEnabled = true;
        LaterButton.IsEnabled = true;
        MessageBox.Show(this,
            string.IsNullOrEmpty(failure) ? "更新に失敗しました。" : failure,
            "WorkTime", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnLater(object sender, RoutedEventArgs e)
    {
        try { _cts.Cancel(); } catch { }
        DialogResult = false;
        Close();
    }
}
