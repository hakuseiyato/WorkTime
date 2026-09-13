using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WorkTime.Services;

/// <summary>
/// 起動中のアプリ 1 件。監視対象はここで得たプロセス名 (= 実行ファイル名) に紐づける。
/// 開いているファイルには紐づけない。
/// </summary>
public sealed class RunningApp
{
    /// <summary>拡張子なしのプロセス名。監視対象のキーになる。</summary>
    public string ProcessName { get; init; } = "";

    /// <summary>実行ファイルの説明文。取得できなければプロセス名と同じ。</summary>
    public string DisplayName { get; init; } = "";
}

/// <summary>
/// 「今開いているアプリ」を列挙する。設定画面から監視対象を選ぶために使う。
/// </summary>
public static class RunningAppScanner
{
    /// <summary>
    /// Windows のシェル基盤プロセス。ウィンドウを持つが「作業に使うアプリ」ではないので出さない。
    /// ApplicationFrameHost は UWP アプリの入れ物で、タイトルだけ中身のアプリ名になるため紛らわしい。
    /// </summary>
    private static readonly HashSet<string> ShellHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost", "TextInputHost", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "SearchApp", "LockApp", "Widgets", "WidgetBoard"
    };

    /// <summary>
    /// ウィンドウを持つプロセスを表示名順に返す。
    /// 常駐サービス・シェル基盤・WorkTime 自身は除外し、プロセス名で重複排除する。
    /// </summary>
    public static List<RunningApp> Scan()
    {
        var found = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        int selfId = Environment.ProcessId;

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return new List<RunningApp>(); }

        foreach (var p in processes)
        {
            using (p)
            {
                try
                {
                    if (p.Id == selfId) continue;
                    // ウィンドウを持たないものは常駐サービス扱いで出さない
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    if (string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;

                    var name = ProcessMonitor.NormalizeProcessName(p.ProcessName);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (ShellHosts.Contains(name)) continue;
                    if (found.ContainsKey(name)) continue; // Chrome 系は同名が大量に並ぶ

                    found[name] = new RunningApp
                    {
                        ProcessName = name,
                        DisplayName = DescribeApp(p, name)
                    };
                }
                catch
                {
                    // アクセスできないプロセスは黙って飛ばす
                }
            }
        }

        return found.Values.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// 実行ファイルの FileDescription を表示名に使う (VS Code なら "Visual Studio Code")。
    /// 保護されたプロセスでは MainModule へのアクセスが拒否されるので、その場合は
    /// プロセス名で代用する。
    /// </summary>
    private static string DescribeApp(Process p, string fallback)
    {
        try
        {
            var description = p.MainModule?.FileVersionInfo?.FileDescription;
            if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
        }
        catch
        {
            // アクセス拒否・ビット数違いなど。fallback を使う
        }
        return fallback;
    }
}
