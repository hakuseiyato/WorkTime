using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace WorkTime;

public partial class App : Application
{
    public static WinForms.NotifyIcon? Tray { get; private set; }
    public static bool IsExiting { get; private set; }

    private MainWindow? _main;
    private bool _startMinimized;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // 起動引数: --tray なら最小化起動 (自動起動向け)
        foreach (var a in e.Args)
        {
            if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))
                _startMinimized = true;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show("エラー: " + ex.Exception.Message, "WorkTime", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        _main = new MainWindow();
        InitTray();

        if (_startMinimized)
        {
            _main.Hide();
        }
        else
        {
            _main.Show();
        }
    }

    private void InitTray()
    {
        Tray = new WinForms.NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "WorkTime",
            Visible = true
        };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => ShowMain());
        menu.Items.Add("計測ON/OFF", null, (_, _) =>
        {
            if (_main?.ViewModel.ToggleCommand.CanExecute(null) == true)
                _main.ViewModel.ToggleCommand.Execute(null);
        });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => RequestExit());
        Tray.ContextMenuStrip = menu;
        Tray.DoubleClick += (_, _) => ShowMain();
    }

    private void ShowMain()
    {
        if (_main == null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Activate();
        _main.Topmost = true;
        _main.Topmost = false;
    }

    private void RequestExit()
    {
        IsExiting = true;
        _main?.ViewModel.Shutdown();
        if (Tray != null)
        {
            Tray.Visible = false;
            Tray.Dispose();
        }
        Shutdown();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (Tray != null)
        {
            Tray.Visible = false;
            Tray.Dispose();
            Tray = null;
        }
    }

    /// <summary>
    /// アイコンファイルを同梱しないので、簡易にビットマップから生成する。
    /// </summary>
    private static Icon BuildIcon()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(255, 15, 15, 18));
            g.FillRectangle(bg, 0, 0, size, size);
            using var bar = new SolidBrush(Color.FromArgb(255, 236, 75, 123));
            g.FillRectangle(bar, 11, 6, 5, 20);
            using var dot = new SolidBrush(Color.White);
            g.FillRectangle(dot, 21, 8, 4, 4);
        }
        IntPtr hIcon = bmp.GetHicon();
        // 所有権を移譲してクローン化 (元 GDI ハンドルは破棄)
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
