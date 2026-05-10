using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// 監視対象プロセス一覧から「現在起動中の対象」を返す。
/// </summary>
public class ProcessMonitor
{
    public List<TrackedProcess> Targets { get; set; } = new();

    /// <summary>
    /// 起動中の対象プロセスのうち、最初にヒットしたものを返す。
    /// 複数同時起動時はリスト上位を優先。
    /// </summary>
    public TrackedProcess? FindRunningTarget()
    {
        if (Targets.Count == 0) return null;

        // 起動中プロセス名の集合 (大文字小文字無視) を 1 度だけ作る
        HashSet<string> running;
        try
        {
            running = Process.GetProcesses()
                .Select(p =>
                {
                    try { return p.ProcessName; }
                    catch { return ""; }
                })
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }

        foreach (var t in Targets)
        {
            if (!t.Enabled) continue;
            if (string.IsNullOrWhiteSpace(t.ProcessName)) continue;
            if (running.Contains(t.ProcessName))
                return t;
        }
        return null;
    }
}
