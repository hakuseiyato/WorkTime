using System;

namespace WorkTime.Models;

/// <summary>
/// 監視対象フォルダの定義。配下のファイルを開いていれば計測を開始する。
/// </summary>
public class TrackedFolder
{
    /// <summary>監視対象フォルダの絶対パス。この配下のファイルを開いていれば計測開始。</summary>
    public string Path { get; set; } = "";

    /// <summary>表示・集計用のプロジェクト名。空ならフォルダ名を使う。</summary>
    public string DisplayName { get; set; } = "";

    public bool Enabled { get; set; } = true;
}
