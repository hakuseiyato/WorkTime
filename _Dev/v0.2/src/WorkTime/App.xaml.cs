using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace WorkTime;

public partial class App : Application
{
    public static WinForms.NotifyIcon? Tray { get; private set; }
    public static bool IsExiting { get; private set; }

    private const string SingleInstanceMutexName = @"Global\WorkTime.SingleInstance.v1";
    private static Mutex? _singleInstanceMutex;

    private MainWindow? _main;
    private DispatcherTimer? _trayTooltipTimer;
    private bool _startMinimized;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // ===== 二重起動防止 =====
        bool createdNew;
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out createdNew);
        if (!createdNew)
        {
            // 既存インスタンスを前面化するシグナルを送る
            SingleInstanceSignal.NotifyExisting();
            Shutdown();
            return;
        }

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
        InitTooltipTimer();
        SingleInstanceSignal.StartListening(ShowMain);

        if (_startMinimized)
            _main.Hide();
        else
            _main.Show();
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

    private void InitTooltipTimer()
    {
        // ツールチップ (NotifyIcon.Text) は 63 文字制限。短く更新する。
        _trayTooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _trayTooltipTimer.Tick += (_, _) => UpdateTrayTooltip();
        _trayTooltipTimer.Start();
        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        if (Tray == null || _main == null) return;
        var vm = _main.ViewModel;
        string state = vm.IsRunning ? "●" : "○";
        string proj = vm.IsRunning && !string.IsNullOrEmpty(vm.Tracker.CurrentProjectKey)
            ? $" {vm.Tracker.CurrentProjectKey}"
            : "";
        string time = $"{vm.Hours}:{vm.Minutes}:{vm.Seconds}";
        string text = $"WorkTime {state} {time}{proj}";
        if (text.Length > 63) text = text.Substring(0, 63);
        Tray.Text = text;
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
        _trayTooltipTimer?.Stop();
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
        SingleInstanceSignal.StopListening();
        if (Tray != null)
        {
            Tray.Visible = false;
            Tray.Dispose();
            Tray = null;
        }
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch { /* 既に解放されている場合は無視 */ }
        _singleInstanceMutex?.Dispose();
    }

    /// <summary>
    /// 埋め込みリソースからマルチサイズ ICO を読み込む。
    /// </summary>
    private static Icon BuildIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/WorkTime.ico", UriKind.Absolute);
            var sri = Application.GetResourceStream(uri);
            if (sri != null)
            {
                using var s = sri.Stream;
                return new Icon(s);
            }
        }
        catch
        {
            // フォールバックへ
        }

        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(255, 26, 27, 31));
            g.FillRectangle(bg, 0, 0, size, size);
            using var bar = new SolidBrush(Color.FromArgb(255, 91, 184, 209));
            g.FillRectangle(bar, 11, 6, 5, 20);
            using var dot = new SolidBrush(Color.White);
            g.FillRectangle(dot, 21, 8, 4, 4);
        }
        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
