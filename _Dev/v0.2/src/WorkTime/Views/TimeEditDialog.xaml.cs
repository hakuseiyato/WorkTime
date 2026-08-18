using System;
using System.Windows;
using WorkTime.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace WorkTime.Views;

/// <summary>計測中セッションの開始時刻を HH:mm:ss で修正する小ダイアログ。</summary>
public partial class TimeEditDialog : Window
{
    /// <summary>OK で確定した時刻。キャンセル時は null。</summary>
    public TimeSpan? Result { get; private set; }
    public TimeEditDialog(TimeSpan initial)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        TimeBox.Text = initial.ToString(@"hh\:mm\:ss");
        Loaded += (_, _) => { TimeBox.Focus(); TimeBox.SelectAll(); };
    }
    private void OnOk(object sender, RoutedEventArgs e)
    {
        // 不正な書式は OK 時に弾く
        if (!TimeSpan.TryParseExact(TimeBox.Text.Trim(), new[] { @"hh\:mm\:ss", @"h\:mm\:ss" }, (IFormatProvider?)null, out var parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
        {
            MessageBox.Show(this, "時刻は HH:mm:ss の形式で入力してください。", "WorkTime", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = parsed;
        DialogResult = true;
        Close();
    }
    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
