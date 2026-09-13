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
            var name = NormalizeProcessName(t.ProcessName);
            if (string.IsNullOrEmpty(name)) continue;
            if (running.Contains(name))
                return t;
        }
        return null;
    }

    /// <summary>
    /// 設定に書かれたプロセス名を比較用に正規化する (前後の空白と末尾の .exe を落とす)。
    /// .NET の Process.ProcessName は拡張子を含まないが、タスクマネージャーの表示は
    /// "Code.exe" なので、そこからコピーした設定が永久に一致せず
    /// 「対象アプリを登録したのに監視下に入らない」状態になる (Issue #1)。
    /// OpenFileMonitor の対象アプリ判定とも共有する。
    /// </summary>
    internal static string NormalizeProcessName(string? name)
    {
        var s = (name ?? "").Trim();
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(0, s.Length - 4);
        return s;
    }
}
