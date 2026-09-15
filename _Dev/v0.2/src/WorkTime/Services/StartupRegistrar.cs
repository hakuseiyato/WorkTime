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
    private const string ApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

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

    /// <summary>
    /// Windows のスタートアップ管理 (タスクマネージャーの「スタートアップ アプリ」や
    /// 設定 → アプリ → スタートアップ) で無効にされているか。
    ///
    /// Run に値があっても、この StartupApproved 側のフラグが立っていると
    /// Windows は値を読み飛ばす。つまり「登録済みなのに起動しない」状態になり、
    /// Run だけを見ていると気づけない。
    /// 先頭バイトの bit0 が 1 なら無効。
    /// </summary>
    public static bool IsDisabledByWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            if (key?.GetValue(ValueName) is not byte[] flags || flags.Length == 0) return false;
            return (flags[0] & 1) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Windows 側の無効フラグを解除する。ユーザーが意図的に切った設定なので、
    /// 起動時に黙って書き戻すことはせず、設定画面から明示的に呼ばれたときだけ実行する。
    /// 成否を返す。
    /// </summary>
    public static bool EnableInWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ApprovedKey, writable: true);
            if (key == null) return false;
            // 先頭バイト 2 = 有効。残りは Windows が使う有効化時刻で、0 で問題ない。
            key.SetValue(ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            return !IsDisabledByWindows();
        }
        catch
        {
            return false;
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
