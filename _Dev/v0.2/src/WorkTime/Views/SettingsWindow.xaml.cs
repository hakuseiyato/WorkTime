using System.Linq;
using System.Windows;
using WorkTime.Models;
using WorkTime.Services;
using WinForms = System.Windows.Forms;

namespace WorkTime.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.Commit();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 「参照…」ボタン: フォルダ選択ダイアログで監視フォルダのパスを設定する。
    /// </summary>
    /// <summary>
    /// 起動中のアプリ一覧から監視対象を選ぶ。手で入力させないことで、
    /// 拡張子つきや綴り違いで永久に一致しない登録が生まれるのを防ぐ。
    /// </summary>
    private void OnPickRunningApps(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        var dlg = new AppPickerDialog(vm.Processes) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        // Processes は ObservableCollection なので、追加・削除はそのまま一覧に反映される
        vm.MergePickedApps(dlg.Result.Select(a => (a.ProcessName, a.DisplayName, a.IsSelected)));
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not TrackedFolder folder) return;

        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "監視するフォルダを選択",
            UseDescriptionForTitle = true,
            SelectedPath = folder.Path
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
        folder.Path = dlg.SelectedPath;

        // TrackedFolder は変更通知を持たないため、表示を明示的に更新する
        var lv = FindAncestorListView(btn);
        lv?.Items.Refresh();
    }

    /// <summary>録画の出力先フォルダを選択する。</summary>
    private void OnBrowseRecordingRoot(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "録画の出力先フォルダを選択",
            UseDescriptionForTitle = true,
            SelectedPath = vm.Recording.OutputRoot
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
        vm.Recording.OutputRoot = dlg.SelectedPath;

        // RecordingConfig は変更通知を持たないため、表示を明示的に更新する
        RecordingOutputRootBox.Text = dlg.SelectedPath;
    }

    private static System.Windows.Controls.ListView? FindAncestorListView(DependencyObject child)
    {
        DependencyObject? current = child;
        while (current != null)
        {
            if (current is System.Windows.Controls.ListView listView) return listView;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
