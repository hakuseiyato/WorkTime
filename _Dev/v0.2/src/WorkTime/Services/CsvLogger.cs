using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// 月ごと CSV ログ (data/logs/YYYY-MM.csv) の入出力を担う。
/// </summary>
public class CsvLogger
{
    public string LogDir { get; }

    public CsvLogger(string? logDir = null)
    {
        LogDir = logDir ?? Path.Combine(AppContext.BaseDirectory, "data", "logs");
        Directory.CreateDirectory(LogDir);
    }

    public string GetLogPath(DateTime forDate)
    {
        return Path.Combine(LogDir, $"{forDate:yyyy-MM}.csv");
    }

    public void Append(SessionRecord record)
    {
        if (record.Duration.TotalSeconds < 1) return; // 1 秒未満は捨てる

        var path = GetLogPath(record.StartTime);
        bool isNew = !File.Exists(path);
        using var sw = new StreamWriter(path, append: true, Encoding.UTF8);
        if (isNew)
            sw.WriteLine(SessionRecord.CsvHeader);
        sw.WriteLine(record.ToCsvRow());
    }

    /// <summary>
    /// 指定範囲 [from, to) の全レコードを読み込む。
    /// </summary>
    public List<SessionRecord> Load(DateTime from, DateTime to)
    {
        var result = new List<SessionRecord>();
        // 月ファイルを走査
        var cursor = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1).AddMonths(1);
        while (cursor < end)
        {
            var path = GetLogPath(cursor);
            if (File.Exists(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path, Encoding.UTF8);
                    bool first = true;
                    foreach (var line in lines)
                    {
                        if (first) { first = false; continue; } // ヘッダ行
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var rec = SessionRecord.TryParseCsvRow(line);
                        if (rec == null) continue;
                        if (rec.EndTime <= from || rec.StartTime >= to) continue;
                        result.Add(rec);
                    }
                }
                catch { /* 壊れた行は無視 */ }
            }
            cursor = cursor.AddMonths(1);
        }
        return result;
    }

    /// <summary>打刻マーカーの月次ログパス (data/logs/markers-YYYY-MM.csv)。</summary>
    public string GetMarkerLogPath(DateTime forDate)
    {
        return Path.Combine(LogDir, $"markers-{forDate:yyyy-MM}.csv");
    }

    /// <summary>打刻マーカーを 1 件追記する。作業時間の集計には一切影響しない。</summary>
    public void AppendMarker(MarkerRecord rec)
    {
        var path = GetMarkerLogPath(rec.Timestamp);
        bool isNew = !File.Exists(path);
        using var sw = new StreamWriter(path, append: true, Encoding.UTF8);
        if (isNew)
            sw.WriteLine(MarkerRecord.CsvHeader);
        sw.WriteLine(rec.ToCsvRow());
    }

    /// <summary>
    /// 指定範囲 [from, to) の打刻マーカーを読み込む。
    /// </summary>
    public List<MarkerRecord> LoadMarkers(DateTime from, DateTime to)
    {
        var result = new List<MarkerRecord>();
        // 月ファイルを走査
        var cursor = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1).AddMonths(1);
        while (cursor < end)
        {
            var path = GetMarkerLogPath(cursor);
            if (File.Exists(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path, Encoding.UTF8);
                    bool first = true;
                    foreach (var line in lines)
                    {
                        if (first) { first = false; continue; } // ヘッダ行
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var rec = MarkerRecord.TryParseCsvRow(line);
                        if (rec == null) continue;
                        if (rec.Timestamp < from || rec.Timestamp >= to) continue;
                        result.Add(rec);
                    }
                }
                catch { /* 壊れた行は無視 */ }
            }
            cursor = cursor.AddMonths(1);
        }
        return result;
    }

    /// <summary>
    /// 指定月の全レコードを別ファイルにエクスポート。
    /// </summary>
    public void ExportMonth(DateTime month, string destPath)
    {
        var src = GetLogPath(month);
        if (!File.Exists(src))
            throw new FileNotFoundException($"対象月のログが存在しません: {src}");
        File.Copy(src, destPath, overwrite: true);
    }
}
