using System;
using Microsoft.Win32;

namespace WorkTime.Services;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run への登録/解除。
/// </summary>
public static class StartupRegistrar
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorkTime";

    /// <summary>
    /// LaunchAtStartup が有効なのに登録先が現在の exe と違う場合、登録し直す。
    /// フォルダ移動による登録の孤児化を、起動のたびに自己修復する。
    /// </summary>
    public static void RepairIfNeeded(bool launchAtStartup, string exePath)
    {
        if (!launchAtStartup) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return;
            var expected = $"\"{exePath}\" --tray";
            if (!string.Equals(key.GetValue(ValueName) as string, expected, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, expected);
        }
        catch
        {
            // 失敗は致命ではないので握りつぶす
        }
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enabled)
                key.SetValue(ValueName, $"\"{exePath}\" --tray");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName);
        }
        catch
        {
            // 失敗は致命ではないので握りつぶす
        }
    }
}
