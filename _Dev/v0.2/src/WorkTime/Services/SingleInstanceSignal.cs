using System;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;

namespace WorkTime;

/// <summary>
/// 二重起動時、後発インスタンスから先発インスタンスへ「前面化要求」を送るための名前付きパイプ。
/// </summary>
internal static class SingleInstanceSignal
{
    private const string PipeName = "WorkTime.SingleInstance.Pipe.v1";
    private static CancellationTokenSource? _cts;

    public static void StartListening(Action onShowRequested)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var thread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    var waitTask = server.WaitForConnectionAsync(token);
                    waitTask.Wait(token);
                    if (token.IsCancellationRequested) break;
                    // 何バイトかは見ない。接続自体がシグナル。
                    Application.Current?.Dispatcher.BeginInvoke(onShowRequested);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    // パイプエラー時は少し待ってリトライ
                    Thread.Sleep(200);
                }
            }
        }) { IsBackground = true, Name = "SingleInstanceListener" };
        thread.Start();
    }

    public static void StopListening()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
    }

    /// <summary>
    /// 既存インスタンスへ「前面化してほしい」と通知する。
    /// </summary>
    public static void NotifyExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 1500);
            client.WriteByte(1);
            client.Flush();
        }
        catch
        {
            // 既存が応答しない場合はサイレント
        }
    }
}
