using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WorkTime.Services;

/// <summary>
/// Windows 11 / Windows 10 22H2+ のタイトルバーをダークに切り替える。
/// </summary>
public static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // 1809 ～ 1903

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// 指定 Window のタイトルバーをダークに切り替える。Window.SourceInitialized より後で呼ぶ。
    /// </summary>
    public static void Apply(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();
            int useDark = 1;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            if (hr != 0)
            {
                // 古いビルド向けのフォールバック
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
            }
        }
        catch
        {
            // 非対応 OS では握りつぶす
        }
    }
}
