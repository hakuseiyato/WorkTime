using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WorkTime.Services;

/// <summary>GitHub Releases から見つかった更新。</summary>
public sealed class UpdateInfo
{
    /// <summary>公開されている最新バージョン。</summary>
    public Version Version { get; init; } = new(0, 0);

    /// <summary>リリースのタグ (v0.7.0 など)。表示用。</summary>
    public string Tag { get; init; } = "";

    /// <summary>リリース名。</summary>
    public string Title { get; init; } = "";

    /// <summary>zip の配布 URL。受け入れ条件を満たしたものだけが入る。</summary>
    public string DownloadUrl { get; init; } = "";

    /// <summary>zip のサイズ (バイト)。ダウンロード前に規模を伝えるために使う。</summary>
    public long Size { get; init; }
}

/// <summary>
/// GitHub Releases を見て更新の有無を判定する。
/// 公開リポジトリなので認証は不要。
/// </summary>
public static class UpdateChecker
{
    private const string LatestApi = "https://api.github.com/repos/hakuseiyato/WorkTime/releases/latest";

    /// <summary>
    /// 受け入れる配布 URL の接頭辞。API の応答は外部から来るデータなので、
    /// ここで縛って想定外のホストからダウンロードしないようにする。
    /// </summary>
    private const string AllowedAssetPrefix =
        "https://github.com/hakuseiyato/WorkTime/releases/download/";

    /// <summary>実行中のバージョン。</summary>
    public static Version CurrentVersion
    {
        get
        {
            try
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version;
                // AssemblyVersion は 0.7.0.0 の 4 桁。比較しやすいよう 3 桁に落とす。
                if (v != null) return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
            }
            catch { }
            return new Version(0, 0, 0);
        }
    }

    /// <summary>
    /// 最新リリースを調べ、現在より新しければ UpdateInfo を返す。
    /// 更新がない・通信できない・応答が想定外のいずれでも null を返し、例外は投げない。
    /// 起動を妨げてはいけないため。
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken token = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            // GitHub API は User-Agent 必須
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkTime-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(LatestApi, token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 下書きとプレリリースは対象外
            if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            if (!TryParseTag(tag, out var latest)) return null;
            if (latest <= CurrentVersion) return null;

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var a in assets.EnumerateArray())
            {
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                long size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0;

                if (!url.StartsWith(AllowedAssetPrefix, StringComparison.Ordinal)) continue;
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                return new UpdateInfo
                {
                    Version = latest,
                    Tag = tag,
                    Title = root.TryGetProperty("name", out var rn) ? rn.GetString() ?? tag : tag,
                    DownloadUrl = url,
                    Size = size
                };
            }
        }
        catch
        {
            // 通信不能・API 制限・応答の形が違う、いずれも「更新なし」として扱う
        }
        return null;
    }

    /// <summary>"v0.7.0" や "0.7.0" を Version に変換する。</summary>
    internal static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        // 後ろに付く -beta などは切る
        int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut > 0) s = s[..cut];
        if (!Version.TryParse(s, out var parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, parsed.Build < 0 ? 0 : parsed.Build);
        return true;
    }
}
