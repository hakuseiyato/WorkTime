using System;

namespace WorkTime.Models;

/// <summary>
/// 1 件の作業セッション (start - end 区間) を表す CSV 行データ。
/// </summary>
public class SessionRecord
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string ProjectKey { get; set; } = "";
    public string ProcessName { get; set; } = "";
    /// <summary>"auto" または "manual"</summary>
    public string Source { get; set; } = "auto";

    public TimeSpan Duration => EndTime - StartTime;

    public string DurationSecondsString => ((long)Duration.TotalSeconds).ToString();

    /// <summary>CSV のヘッダ行。</summary>
    public const string CsvHeader = "Date,StartTime,EndTime,DurationSec,ProjectKey,ProcessName,Source";

    public string ToCsvRow()
    {
        // ダブルクォートと改行を素朴にエスケープ
        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        return string.Join(",",
            StartTime.ToString("yyyy-MM-dd"),
            StartTime.ToString("HH:mm:ss"),
            EndTime.ToString("HH:mm:ss"),
            DurationSecondsString,
            Esc(ProjectKey),
            Esc(ProcessName),
            Esc(Source));
    }

    public static SessionRecord? TryParseCsvRow(string line)
    {
        // 厳密 CSV パーサ。ダブルクォート対応。
        var fields = ParseCsvLine(line);
        if (fields.Count < 7) return null;

        try
        {
            var date = DateTime.ParseExact(fields[0], "yyyy-MM-dd", null);
            var start = TimeSpan.Parse(fields[1]);
            var end = TimeSpan.Parse(fields[2]);
            return new SessionRecord
            {
                StartTime = date.Add(start),
                EndTime = date.Add(end),
                ProjectKey = fields[4],
                ProcessName = fields[5],
                Source = fields[6]
            };
        }
        catch
        {
            return null;
        }
    }

    private static System.Collections.Generic.List<string> ParseCsvLine(string line)
    {
        var result = new System.Collections.Generic.List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
