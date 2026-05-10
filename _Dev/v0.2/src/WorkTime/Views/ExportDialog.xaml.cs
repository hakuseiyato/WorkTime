using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WorkTime.Services;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace WorkTime.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    private ExportViewModel? VM => DataContext as ExportViewModel;

    private void OnPresetToday(object sender, RoutedEventArgs e) => VM?.ApplyPreset("today");
    private void OnPresetWeek(object sender, RoutedEventArgs e) => VM?.ApplyPreset("week");
    private void OnPresetMonth(object sender, RoutedEventArgs e) => VM?.ApplyPreset("month");
    private void OnPresetLastMonth(object sender, RoutedEventArgs e) => VM?.ApplyPreset("lastMonth");
    private void OnPresetAll(object sender, RoutedEventArgs e) => VM?.ApplyPreset("all");
    private void OnSelectAll(object sender, RoutedEventArgs e) => VM?.SelectAll(true);
    private void OnSelectNone(object sender, RoutedEventArgs e) => VM?.SelectAll(false);

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (VM == null) return;
        if (VM.SelectedSessionCount == 0)
        {
            MessageBox.Show(this, "選択されたプロジェクトにセッションが 0 件です。",
                "WorkTime", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "CSV を保存",
            FileName = $"WorkTime_{VM.FromDate:yyyyMMdd}-{VM.ToDate:yyyyMMdd}.csv",
            Filter = "CSV ファイル|*.csv|すべてのファイル|*.*",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            VM.ExportTo(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"エクスポートに失敗しました: {ex.Message}",
                "WorkTime", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(this,
            $"エクスポート完了\n{dlg.FileName}\n\n出力ファイルを開きますか?",
            "WorkTime", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch { /* 関連付けが無いだけ */ }
        }
    }
}
