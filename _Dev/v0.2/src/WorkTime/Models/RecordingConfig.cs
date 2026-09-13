namespace WorkTime.Models;

/// <summary>タイムラプス録画とクリップ生成の設定。</summary>
public class RecordingConfig
{
    /// <summary>セッション連動録画を有効にする。</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>録画ファイルの保存先ルート。</summary>
    public string OutputRoot { get; set; } = @"F:\timelapse\tl_all\rec";

    /// <summary>毎秒の撮影フレーム数。</summary>
    public int Fps { get; set; } = 1;

    /// <summary>出力画像の基準サイズ。</summary>
    public int LongEdge { get; set; } = 1920;

    /// <summary>録画品質。小さいほど高画質。</summary>
    public int Crf { get; set; } = 30;

    /// <summary>録画エンコーダー (libx264 / h264_nvenc)。</summary>
    public string Encoder { get; set; } = "libx264";

    /// <summary>アイドル中はセッション連動録画を一時停止する。</summary>
    public bool PauseOnIdle { get; set; } = true;

    /// <summary>録画停止時にクリップを自動生成する。</summary>
    public bool AutoClipOnSessionEnd { get; set; } = true;

    /// <summary>早回しクリップの目標秒数。</summary>
    public int ClipTargetSeconds { get; set; } = 75;

    /// <summary>ffmpeg のファイルまたはディレクトリ。空なら PATH から検索する。</summary>
    public string FfmpegPath { get; set; } = "";
}
