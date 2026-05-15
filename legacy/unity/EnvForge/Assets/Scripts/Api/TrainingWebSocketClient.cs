using System;
using System.Collections.Concurrent;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

public class TrainingWebSocketClient : IDisposable
{
    private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
#if !UNITY_WEBGL || UNITY_EDITOR
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellation;
    private Task receiveTask;

    public bool IsRunning => receiveTask != null && !receiveTask.IsCompleted;
#else
    public bool IsRunning => false;
#endif

    public void Connect(string url)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        Stop();

        cancellation = new CancellationTokenSource();
        webSocket = new ClientWebSocket();
        receiveTask = Run(url, cancellation.Token);
#else
        errors.Enqueue("ClientWebSocket is not supported in WebGL builds.");
#endif
    }

    public bool TryDequeueMessage(out string message)
    {
        return messages.TryDequeue(out message);
    }

    public bool TryDequeueError(out string error)
    {
        return errors.TryDequeue(out error);
    }

    public void Stop()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }

        if (webSocket != null)
        {
            webSocket.Dispose();
            webSocket = null;
        }

        receiveTask = null;
#endif
    }

    public void Dispose()
    {
        Stop();
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private async Task Run(string url, CancellationToken token)
    {
        try
        {
            await webSocket.ConnectAsync(new Uri(url), token);
            await ReceiveMessages(token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            errors.Enqueue(ex.Message);
        }
    }

    private async Task ReceiveMessages(CancellationToken token)
    {
        byte[] buffer = new byte[8192];

        while (!token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            var builder = new StringBuilder();
            WebSocketReceiveResult result;

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                    return;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            messages.Enqueue(builder.ToString());
        }
    }
#endif
}
