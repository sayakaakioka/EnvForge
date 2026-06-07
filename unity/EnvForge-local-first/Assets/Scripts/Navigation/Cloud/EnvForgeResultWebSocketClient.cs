using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeResultWebSocketClient : IDisposable
    {
        private readonly object syncRoot = new();
        private readonly Queue<string> receivedMessages = new();

        private ClientWebSocket socket;
        private CancellationTokenSource cancellation;
        private Task receiveTask;
        private string lastError;

        public bool IsRunning => receiveTask != null && !receiveTask.IsCompleted;

        public string LastError
        {
            get
            {
                lock (syncRoot)
                {
                    return lastError;
                }
            }
        }

        public void Start(string url)
        {
            Stop();
            ClearMessages();
            if (string.IsNullOrWhiteSpace(url))
            {
                SetError("WebSocket URL is empty");
                return;
            }

            SetError(null);
            cancellation = new CancellationTokenSource();
            socket = new ClientWebSocket();
            receiveTask = Task.Run(() => RunAsync(url, cancellation.Token));
        }

        public void Stop()
        {
            try
            {
                cancellation?.Cancel();
                socket?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Result WebSocket stop failed: {ex.Message}");
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                socket = null;
                receiveTask = null;
            }
        }

        public bool TryDequeue(out string message)
        {
            lock (syncRoot)
            {
                if (receivedMessages.Count == 0)
                {
                    message = null;
                    return false;
                }

                message = receivedMessages.Dequeue();
                return true;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task RunAsync(string url, CancellationToken token)
        {
            ClientWebSocket activeSocket = socket;
            try
            {
                await activeSocket.ConnectAsync(new Uri(url), token).ConfigureAwait(false);
                await ReceiveLoopAsync(activeSocket, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket activeSocket, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            while (!token.IsCancellationRequested && activeSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                StringBuilder builder = new();
                do
                {
                    result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                Enqueue(builder.ToString());
            }
        }

        private void ClearMessages()
        {
            lock (syncRoot)
            {
                receivedMessages.Clear();
            }
        }

        private void Enqueue(string message)
        {
            lock (syncRoot)
            {
                receivedMessages.Enqueue(message);
            }
        }

        private void SetError(string error)
        {
            lock (syncRoot)
            {
                lastError = error;
            }
        }
    }
}
