using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>録画済みセグメントを約 N 秒の早回しクリップに変換する。</summary>
public static class ClipMaker
{
    /// <summary>成功したら出力パス、失敗なら null を返す。重い処理なので呼び出し側で別スレッドに逃がすこと。</summary>
    public static string? Make(RecordingConfig cfg, string sourcePath)
    {
        try
        {
            var result = ScreenRecorder.Probe(cfg, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", sourcePath);
            if (!double.TryParse(result?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
                || !double.IsFinite(duration) || duration <= 0) return null;
            var targetSeconds = cfg.ClipTargetSeconds > 0 ? cfg.ClipTargetSeconds : 75;
            var speed = (int)Math.Clamp(duration / targetSeconds, 1, int.MaxValue);
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
            if (sourceDirectory == null) return null;
            var directory = Path.Combine(sourceDirectory, "clips");
            Directory.CreateDirectory(directory);
            var outputPath = ScreenRecorder.UniquePath(Path.Combine(directory,
                Path.GetFileNameWithoutExtension(sourcePath) + "_x" + ScreenRecorder.Number(speed) + ".mp4"));
            var startInfo = ScreenRecorder.CreateStartInfo(cfg, "ffmpeg");
            ScreenRecorder.AddArguments(startInfo, "-hide_banner", "-loglevel", "warning", "-y", "-i", sourcePath,
                "-vf", "setpts=PTS/" + ScreenRecorder.Number(speed) + ",fps=30,scale=" + ScreenRecorder.Number(ScreenRecorder.ScaleWidth(cfg)) + ":-2",
                "-an", "-c:v", "libx264", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p", outputPath);
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) return null;
            process.WaitForExit();
            return process.ExitCode == 0 && File.Exists(outputPath) ? outputPath : null;
        }
        catch
        {
            return null;
        }
    }
}
