using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WorkTime.Services;

/// <summary>
/// ダウンロードした exe で自分自身を置き換える。
///
/// 実行中の exe は上書きできないが、リネームはできる (NTFS の性質)。
/// これを使うと外部のバッチを噛ませずに置換できる。
/// </summary>
public static class SelfUpdater
{
    /// <summary>置換前の exe を退避する拡張子。次回起動時に掃除する。</summary>
    private const string OldSuffix = ".old";

    /// <summary>
    /// 前回の更新で残った旧 exe を削除する。起動時に呼ぶ。
    /// 消せなくても無視する (次回また試せばよい)。
    /// </summary>
    public static void CleanupOldFile()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            var old = exe + OldSuffix;
            if (File.Exists(old)) File.Delete(old);
        }
        catch
        {
            // 掴まれている場合など。次回に回す
        }
    }

    /// <summary>
    /// 更新を適用する。成功したら新しい exe を起動して true を返す
    /// (呼び出し側は速やかに自分を終了させること)。
    /// 失敗したら元の状態へ戻して false を返し、reason に理由が入る。
    /// </summary>
    public static async Task<bool> ApplyAsync(
        UpdateInfo info,
        IProgress<double>? progress,
        Action<string> reason,
        CancellationToken token = default)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            reason("実行中の exe のパスを特定できませんでした。");
            return false;
        }

        var work = Path.Combine(Path.GetTempPath(), "WorkTimeUpdate_" + Guid.NewGuid().ToString("N"));
        var old = exe + OldSuffix;
        bool renamed = false;

        try
        {
            Directory.CreateDirectory(work);

            // 1) zip を取得
            var zipPath = Path.Combine(work, "package.zip");
            if (!await DownloadAsync(info.DownloadUrl, zipPath, progress, token).ConfigureAwait(false))
            {
                reason("ダウンロードに失敗しました。");
                return false;
            }

            // 2) zip から exe を取り出す
            var newExe = ExtractExe(zipPath, work);
            if (newExe == null)
            {
                reason("ダウンロードした zip に WorkTime.exe が入っていませんでした。");
                return false;
            }
            if (new FileInfo(newExe).Length < 1_000_000)
            {
                reason("取り出した exe のサイズが小さすぎます。ファイルが壊れている可能性があります。");
                return false;
            }

            // 3) 実行中の exe をリネームして退避 (上書きはできないがリネームはできる)
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            renamed = true;

            // 4) 新しい exe を配置
            File.Copy(newExe, exe, overwrite: true);

            // 5) 新しい方をトレイ常駐で起動
            Process.Start(new ProcessStartInfo(exe, "--tray") { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            // 置換の途中で失敗したら元に戻す。ここで戻せないと起動できなくなる。
            if (renamed)
            {
                try
                {
                    if (File.Exists(exe)) File.Delete(exe);
                    File.Move(old, exe);
                }
                catch
                {
                    reason("更新に失敗し、元の exe も復旧できませんでした。" +
                           $"手動で {Path.GetFileName(old)} を {Path.GetFileName(exe)} に戻してください。");
                    return false;
                }
            }
            reason("更新に失敗しました: " + ex.Message);
            return false;
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }

    private static async Task<bool> DownloadAsync(
        string url, string dest, IProgress<double>? progress, CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkTime-Updater");

            using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
                                      .ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return false;

            var total = res.Content.Headers.ContentLength ?? 0;
            using var src = await res.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var dst = File.Create(dest);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>zip から WorkTime.exe を取り出す。見つからなければ null。</summary>
    private static string? ExtractExe(string zipPath, string workDir)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(
                e => string.Equals(Path.GetFileName(e.FullName), "WorkTime.exe", StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;

            var dest = Path.Combine(workDir, "WorkTime.exe");
            entry.ExtractToFile(dest, overwrite: true);
            return dest;
        }
        catch
        {
            return null;
        }
    }
}
