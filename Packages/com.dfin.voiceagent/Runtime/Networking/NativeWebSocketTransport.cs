using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NativeWebSocket;

namespace DFIN.VoiceAgent.Networking
{
    /// <summary>
    /// WebSocket transport built on top of the NativeWebSocket package for broad platform support (desktop, mobile, Quest, WebGL).
    /// </summary>
    public sealed class NativeWebSocketTransport : IRealtimeTransport
    {
        public event Action Connected;
        public event Action<string> TextMessageReceived;
        public event Action<byte[]> BinaryMessageReceived;
        public event Action<Exception> Error;
        public event Action Closed;

        private WebSocket webSocket;
        private readonly object sync = new();

        private bool disposed;

        public async Task ConnectAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, string> headerCopy = null;
            if (headers != null && headers.Count > 0)
            {
                headerCopy = new Dictionary<string, string>(headers);
            }

            lock (sync)
            {
                if (webSocket != null)
                {
                    throw new InvalidOperationException("WebSocket is already connected or connecting.");
                }

                webSocket = headerCopy != null
                    ? new WebSocket(uri.ToString(), headerCopy)
                    : new WebSocket(uri.ToString());
            }

            RegisterCallbacks(webSocket);

            using var registration = cancellationToken.Register(CancelConnect);

            try
            {
                await webSocket.Connect();
            }
            catch
            {
                DisposeWebSocket();
                throw;
            }
        }

        public Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var socket = EnsureConnected();
            return socket.SendText(message);
        }

        public Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = EnsureConnected();
            if (data.Array == null)
            {
                return socket.Send(Array.Empty<byte>());
            }

            if (data.Offset == 0 && data.Count == data.Array.Length)
            {
                return socket.Send(data.Array);
            }

            var temp = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, temp, 0, data.Count);
            return socket.Send(temp);
        }

        public async Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = webSocket;
            if (socket == null)
            {
                return;
            }

            try
            {
                await socket.Close();
            }
            finally
            {
                DisposeWebSocket();
            }
        }

        public void DispatchMessageQueue()
        {
            webSocket?.DispatchMessageQueue();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeWebSocket();
        }

        private WebSocket EnsureConnected()
        {
            var socket = webSocket;
            if (socket == null)
            {
                throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
            }

            return socket;
        }

        private void RegisterCallbacks(WebSocket socket)
        {
            socket.OnOpen += HandleOpen;
            socket.OnMessage += HandleMessage;
            socket.OnError += HandleError;
            socket.OnClose += HandleClose;
        }

        private void UnregisterCallbacks(WebSocket socket)
        {
            if (socket == null)
            {
                return;
            }

            socket.OnOpen -= HandleOpen;
            socket.OnMessage -= HandleMessage;
            socket.OnError -= HandleError;
            socket.OnClose -= HandleClose;
        }

        private void HandleOpen()
        {
            Connected?.Invoke();
        }

        private void HandleMessage(byte[] data)
        {
            if (data == null)
            {
                return;
            }

            if (data.Length > 0)
            {
                BinaryMessageReceived?.Invoke(data);
            }

            var text = Encoding.UTF8.GetString(data);
            TextMessageReceived?.Invoke(text);
        }

        private void HandleError(string message)
        {
            Error?.Invoke(new Exception(message));
        }

        private void HandleClose(WebSocketCloseCode closeCode)
        {
            Closed?.Invoke();
        }

        private void CancelConnect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // NativeWebSocket doesn't support cancellation on WebGL builds.
#else
            webSocket?.CancelConnection();
#endif
        }

        private void DisposeWebSocket()
        {
            WebSocket socket;
            lock (sync)
            {
                socket = webSocket;
                webSocket = null;
            }

            if (socket == null)
            {
                return;
            }

            UnregisterCallbacks(socket);
            socket.CancelConnection();
        }
    }
}
