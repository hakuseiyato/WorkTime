using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using WorkTime.Models;

namespace WorkTime.Services;

/// <summary>
/// ffmpeg を子プロセスとして起動し、デスクトップの指定矩形をタイムラプス録画する。
/// </summary>
public class ScreenRecorder
{
    private Process? _process;
    private string? _currentPath;
    private string? _fileStem;
    private DateTime? _startedAt;

    public bool IsRecording
    {
        get
        {
            try { return _process != null && !_process.HasExited; }
            catch { return false; }
        }
    }

    /// <summary>録画中のセグメントの出力先パス。未録画なら null。</summary>
    public string? CurrentPath => IsRecording ? _currentPath : null;

    /// <summary>録画開始時刻。未録画なら null。</summary>
    public DateTime? StartedAt => IsRecording ? _startedAt : null;

    public event Action? StateChanged;

    /// <summary>録画を開始する。既に録画中なら何もしない。成否を返す。</summary>
    public bool Start(RecordingConfig cfg, CaptureRect rect, string label)
    {
        try
        {
            if (IsRecording) return false;
            if (_process != null) Stop(cfg);

            var startedAt = DateTime.Now;
            var directory = Path.Combine(cfg.OutputRoot, startedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            var invalid = Path.GetInvalidFileNameChars();
            var safeLabel = string.IsNullOrWhiteSpace(label) ? "Session"
                : new string(label.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            _fileStem = safeLabel + "_" + startedAt.ToString("HHmmss", CultureInfo.InvariantCulture);
            _currentPath = UniquePath(Path.Combine(directory, _fileStem + "_recording.mkv"));
            var startInfo = CreateStartInfo(cfg, "ffmpeg");
            startInfo.RedirectStandardInput = true;
            AddArguments(startInfo, "-hide_banner", "-loglevel", "warning",
                "-f", "gdigrab", "-framerate", Number(Math.Max(1, cfg.Fps)),
                "-offset_x", Number(rect.X), "-offset_y", Number(rect.Y),
                "-video_size", Number(rect.Width - rect.Width % 2) + "x" + Number(rect.Height - rect.Height % 2), "-i", "desktop",
                "-vf", "scale=" + Number(ScaleWidth(cfg)) + ":-2");
            if (cfg.Encoder == "h264_nvenc")
                AddArguments(startInfo, "-c:v", "h264_nvenc", "-preset", "p5", "-cq", Number(Math.Clamp(cfg.Crf, 0, 51)));
            else
                AddArguments(startInfo, "-c:v", "libx264", "-preset", "veryfast", "-crf", Number(Math.Clamp(cfg.Crf, 0, 51)));
            AddArguments(startInfo, "-pix_fmt", "yuv420p", "-an", _currentPath);
            _process = new Process { StartInfo = startInfo };
            if (!_process.Start()) throw new InvalidOperationException("録画を開始できませんでした。");
            _startedAt = startedAt;
            NotifyStateChanged();
            return true;
        }
        catch
        {
            if (_process != null) StopProcess(_process);
            ClearState();
            return false;
        }
    }

    /// <summary>録画を停止し、フレーム数を数えてリネームした最終パスを返す。未録画なら null。</summary>
    public string? Stop(RecordingConfig cfg)
    {
        var path = _currentPath;
        try
        {
            if (_process == null) return null;
            StopProcess(_process);
            if (path == null || !File.Exists(path)) return path;
            // -count_frames は全フレームをデコードするため、12 時間の録画で 90 秒以上かかり
            // Probe のタイムアウトに達してリネーム自体が失敗する。-count_packets は
            // デマックスするだけで同じ値を 0.2 秒で返す (416MB / 52566 フレームで実測)。
            var result = Probe(cfg, "-v", "error", "-select_streams", "v:0", "-count_packets",
                "-show_entries", "stream=nb_read_packets", "-of", "csv=p=0", path);
            if (!long.TryParse(result?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) || frames < 0)
                return path;
            var directory = Path.GetDirectoryName(path);
            if (directory == null || _fileStem == null) return path;
            var finalPath = UniquePath(Path.Combine(directory, _fileStem + "_" + frames.ToString(CultureInfo.InvariantCulture) + "f.mkv"));
            File.Move(path, finalPath);
            return finalPath;
        }
        catch
        {
            return path;
        }
        finally
        {
            ClearState();
            NotifyStateChanged();
        }
    }

    private void ClearState()
    {
        try { _process?.Dispose(); }
        catch { }
        _process = null;
        _currentPath = null;
        _fileStem = null;
        _startedAt = null;
    }

    private void NotifyStateChanged()
    {
        try { StateChanged?.Invoke(); }
        catch { }
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.StandardInput.Write("q");
                    process.StandardInput.Flush();
                }
                catch { }
                if (!process.WaitForExit(10000))
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
            }
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(); }
            catch { }
        }
    }

    internal static ProcessStartInfo CreateStartInfo(RecordingConfig cfg, string executable)
    {
        var fileName = executable;
        if (!string.IsNullOrWhiteSpace(cfg.FfmpegPath))
        {
            var directory = File.Exists(cfg.FfmpegPath)
                ? Path.GetDirectoryName(Path.GetFullPath(cfg.FfmpegPath)) : cfg.FfmpegPath;
            fileName = Path.Combine(directory ?? "", executable + ".exe");
        }
        return new ProcessStartInfo(fileName) { UseShellExecute = false, CreateNoWindow = true };
    }

    internal static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    }

    internal static int ScaleWidth(RecordingConfig cfg)
    {
        var width = Math.Max(2, cfg.LongEdge);
        return width - width % 2;
    }

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string UniquePath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var candidate = path;
        for (var suffix = 2; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
            candidate = Path.Combine(directory, stem + "_" + Number(suffix) + extension);
        return candidate;
    }

    internal static string? Probe(RecordingConfig cfg, params string[] arguments)
    {
        using var process = new Process { StartInfo = CreateStartInfo(cfg, "ffprobe") };
        try
        {
            process.StartInfo.RedirectStandardOutput = true;
            AddArguments(process.StartInfo, arguments);
            if (!process.Start()) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(60000)) return null;
            if (process.ExitCode != 0 || !output.Wait(1000)) return null;
            return output.GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
            }
            catch { }
        }
    }
}
