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
