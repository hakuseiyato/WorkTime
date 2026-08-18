using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// 監視対象フォルダ配下のファイルを開いているプロセスを探す。
/// </summary>
public class OpenFileMonitor
{
    private DateTime _lastQueryAt = DateTime.MinValue;
    private TrackedFolder? _lastResult;

    public List<TrackedFolder> Targets { get; set; } = new();

    /// <summary>
    /// ウィンドウタイトルの「フォルダ名だけ」一致を許可するアプリ (= 監視対象プロセス)。
    /// Unity はタイトルにプロジェクト名しか出さないためこの経路が要る。
    /// 一方でエクスプローラやターミナルもフォルダ名をタイトルに出すので、
    /// 対象アプリに限定しないと「見ているだけ」で計測が始まってしまう。
    /// </summary>
    public List<TrackedProcess> KnownApps { get; set; } = new();

    public TrackedFolder? FindOpenTarget()
    {
        if (!Targets.Any(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Path))) return null;

        var now = DateTime.Now;
        if (now - _lastQueryAt < TimeSpan.FromSeconds(10)) return _lastResult;

        // 自分自身は必ず除外する。WorkTime の exe パスやウィンドウタイトルが
        // 監視フォルダに一致すると、何も開いていなくても常時計測になってしまう。
        int selfId = Environment.ProcessId;

        var commandLines = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");
            using var objects = searcher.Get();
            foreach (ManagementObject obj in objects)
            {
                using (obj)
                {
                    if (obj["ProcessId"] is uint pid && pid == selfId) continue;
                    if (obj["CommandLine"] is string commandLine && !string.IsNullOrEmpty(commandLine))
                        commandLines.Add(commandLine);
                }
            }
        }
        catch
        {
            // WMI が利用できなくてもウィンドウタイトルで判定を続ける
        }

        var knownNames = KnownApps
            .Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.ProcessName))
            .Select(a => a.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // フルパス一致は誰のウィンドウでも信用してよい。
        // フォルダ名だけの一致は対象アプリのウィンドウに限る。
        var windowTitles = new List<string>();
        var knownAppTitles = new List<string>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == selfId) continue;
                        var title = process.MainWindowTitle;
                        if (string.IsNullOrEmpty(title)) continue;
                        windowTitles.Add(title);
                        if (knownNames.Contains(process.ProcessName)) knownAppTitles.Add(title);
                    }
                    catch
                    {
                        // アクセスできないプロセスは無視する
                    }
                }
            }
        }
        catch
        {
            // プロセス一覧を取得できない場合は収集済み候補だけで判定する
        }

        TrackedFolder? result = null;
        foreach (var target in Targets)
        {
            if (!target.Enabled || string.IsNullOrWhiteSpace(target.Path)) continue;

            string normalizedPath;
            try
            {
                normalizedPath = System.IO.Path.GetFullPath(target.Path).TrimEnd('\\', '/');
            }
            catch
            {
                continue;
            }

            if (commandLines.Any(s => ContainsIgnoreCase(s, normalizedPath))
                || windowTitles.Any(s => ContainsIgnoreCase(s, normalizedPath)))
            {
                result = target;
                break;
            }

            // フォルダ名が短すぎると誤検知が増えるので 3 文字以上に限る
            var leaf = System.IO.Path.GetFileName(normalizedPath);
            if (leaf.Length > 2 && knownAppTitles.Any(s => ContainsIgnoreCase(s, leaf)))
            {
                result = target;
                break;
            }
        }

        _lastQueryAt = now;
        _lastResult = result;
        return result;
    }

    private static bool ContainsIgnoreCase(string value, string needle)
    {
        return value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
