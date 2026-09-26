using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using WinForms = System.Windows.Forms;

namespace WorkTime.Services;

/// <summary>録画するデスクトップ上の矩形。gdigrab の -offset_x/-offset_y/-video_size に渡す。</summary>
public readonly record struct CaptureRect(int X, int Y, int Width, int Height);

/// <summary>設定画面に出すモニタ 1 台ぶんの情報。</summary>
public sealed class MonitorInfo
{
    /// <summary>設定に保存する値。"auto" / "primary" / デバイス名 (\\.\DISPLAY1 など)。</summary>
    public string Key { get; init; } = "";

    /// <summary>一覧に出す表示名。デバイス名だけではどの画面か分からないので解像度と位置を併記する。</summary>
    public string Label { get; init; } = "";

    public override string ToString() => Label;
}

public static class CaptureRegion
{
    /// <summary>対象アプリが載っているモニタに追従する (既定)。</summary>
    public const string Auto = "auto";

    /// <summary>常にプライマリモニタを録画する。</summary>
    public const string Primary = "primary";

    /// <summary>
    /// 設定で選べるモニタの一覧。先頭 2 件は特別な選択肢で、以降が実際のモニタ。
    /// </summary>
    public static List<MonitorInfo> ListMonitors()
    {
        var list = new List<MonitorInfo>
        {
            new() { Key = Auto,    Label = "自動 (対象アプリのモニタ)" },
            new() { Key = Primary, Label = "プライマリ" },
        };
        try
        {
            int i = 0;
            foreach (var s in WinForms.Screen.AllScreens)
            {
                i++;
                var b = s.Bounds;
                var name = ShortName(s.DeviceName);
                var suffix = s.Primary ? " (プライマリ)" : "";
                list.Add(new MonitorInfo
                {
                    Key = s.DeviceName,
                    Label = $"{i}. {name} — {b.Width}×{b.Height} ({b.X},{b.Y}){suffix}"
                });
            }
        }
        catch
        {
            // 列挙できなければ特別な選択肢だけを返す
        }
        return list;
    }

    /// <summary>
    /// 設定値から録画範囲を決める。セッション連動と手動録画で同じ規則を通すための唯一の入口。
    /// </summary>
    /// <param name="selection">RecordingConfig.Monitor の値。</param>
    /// <param name="process">auto のときに参照する対象プロセス。手動録画では null。</param>
    public static CaptureRect Resolve(string? selection, Process? process)
    {
        var key = string.IsNullOrWhiteSpace(selection) ? Auto : selection.Trim();

        if (string.Equals(key, Auto, StringComparison.OrdinalIgnoreCase))
        {
            // 対象アプリが分かるならそのモニタ。分からない (手動録画など) ならプライマリ。
            if (process != null && ForProcess(process) is CaptureRect r) return r;
            return PrimaryMonitor();
        }

        if (string.Equals(key, Primary, StringComparison.OrdinalIgnoreCase))
            return PrimaryMonitor();

        try
        {
            var hit = WinForms.Screen.AllScreens
                .FirstOrDefault(s => string.Equals(s.DeviceName, key, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return FromBounds(hit.Bounds);
        }
        catch
        {
            // 列挙に失敗したらフォールバックへ
        }

        // 指定したモニタが見つからない (ケーブルを抜いた / 構成が変わった)。
        // ここで録画を止めると「気づかないうちに録れていない」状態になるので、
        // プライマリに落として録画は続ける。
        return PrimaryMonitor();
    }

    /// <summary>指定プロセスのウィンドウが載っているモニタ全体を返す。取得できなければ null。</summary>
    public static CaptureRect? ForProcess(Process process)
    {
        try
        {
            var hwnd = process.MainWindowHandle;
            return hwnd == IntPtr.Zero ? null : FromBounds(WinForms.Screen.FromHandle(hwnd).Bounds);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>プライマリモニタ全体を返す。</summary>
    public static CaptureRect PrimaryMonitor()
    {
        try
        {
            var bounds = WinForms.Screen.PrimaryScreen?.Bounds;
            if (bounds.HasValue) return FromBounds(bounds.Value);
            var screens = WinForms.Screen.AllScreens;
            if (screens.Length > 0) return FromBounds(screens[0].Bounds);
        }
        catch
        {
            // モニタを取得できない場合は既定サイズを使う。
        }
        return FromBounds(new Rectangle(0, 0, 1920, 1080));
    }

    /// <summary>"\\.\DISPLAY1" を "DISPLAY1" に縮めて読みやすくする。</summary>
    private static string ShortName(string deviceName)
    {
        var s = deviceName ?? "";
        int i = s.LastIndexOf('\\');
        return i >= 0 && i < s.Length - 1 ? s[(i + 1)..] : s;
    }

    // 幅・高さは必ず偶数にする。奇数だと libx264 が 0 バイト出力で落ちる。
    private static CaptureRect FromBounds(Rectangle bounds)
        => new(bounds.X, bounds.Y, bounds.Width - bounds.Width % 2, bounds.Height - bounds.Height % 2);
}
