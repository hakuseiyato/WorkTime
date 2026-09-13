using System;
using System.Diagnostics;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace WorkTime.Services;

/// <summary>録画するデスクトップ上の矩形。gdigrab の -offset_x/-offset_y/-video_size に渡す。</summary>
public readonly record struct CaptureRect(int X, int Y, int Width, int Height);

public static class CaptureRegion
{
    /// <summary>指定プロセスのウィンドウが載っているモニタ全体を返す。取得できなければ null。</summary>
    public static CaptureRect? ForProcess(Process process)
    {
        try
        {
            var hwnd = process.MainWindowHandle;
            return hwnd == IntPtr.Zero ? null : FromBounds(WinForms.Screen.FromHandle(hwnd).Bounds);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>プライマリモニタ全体を返す。</summary>
    public static CaptureRect PrimaryMonitor()
    {
        try
        {
            var bounds = WinForms.Screen.PrimaryScreen?.Bounds;
            if (bounds.HasValue) return FromBounds(bounds.Value);
            var screens = WinForms.Screen.AllScreens;
            if (screens.Length > 0) return FromBounds(screens[0].Bounds);
        }
        catch
        {
            // モニタを取得できない場合は既定サイズを使う。
        }
        return FromBounds(new Rectangle(0, 0, 1920, 1080));
    }

    private static CaptureRect FromBounds(Rectangle bounds)
        => new(bounds.X, bounds.Y, bounds.Width - bounds.Width % 2, bounds.Height - bounds.Height % 2);
}
