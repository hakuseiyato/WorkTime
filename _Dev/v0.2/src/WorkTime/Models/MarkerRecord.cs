using System;

namespace WorkTime.Models;

/// <summary>一瞬の打刻マーカー。作業時間の集計には一切影響しない。</summary>
public class MarkerRecord
{
    public DateTime Timestamp { get; set; }
    public string ProjectKey { get; set; } = "";
    public string Memo { get; set; } = "";

    public const string CsvHeader = "Date,Time,ProjectKey,Memo";

    public string ToCsvRow()
    {
        return string.Join(",", Timestamp.ToString("yyyy-MM-dd"), Timestamp.ToString("HH:mm:ss"),
            SessionRecord.Esc(ProjectKey), SessionRecord.Esc(Memo));
    }

    public static MarkerRecord? TryParseCsvRow(string line)
    {
        var fields = SessionRecord.ParseCsvLine(line);
        if (fields.Count < 3) return null;
        try
        {
            var date = DateTime.ParseExact(fields[0], "yyyy-MM-dd", null);
            var time = TimeSpan.Parse(fields[1]);
            return new MarkerRecord
            {
                Timestamp = date.Add(time),
                ProjectKey = fields[2],
                Memo = fields.Count >= 4 ? fields[3] : ""
            };
        }
        catch
        {
            return null;
        }
    }
}
