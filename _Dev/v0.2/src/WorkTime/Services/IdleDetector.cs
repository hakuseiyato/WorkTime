using System;
using System.Runtime.InteropServices;

namespace WorkTime.Services;

/// <summary>
/// Win32 GetLastInputInfo を使った無操作時間取得。
/// </summary>
public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>
    /// 最後のユーザー入力からの経過時間。
    /// </summary>
    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(info);
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // Environment.TickCount は 32bit でラップする可能性があるが、短時間判定なので許容。
        uint idleTicks = (uint)Environment.TickCount - info.dwTime;
        return TimeSpan.FromMilliseconds(idleTicks);
    }
}
