using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DFIN.VoiceAgent.Networking
{
    /// <summary>
    /// Placeholder transport used until the NativeWebSocket implementation is wired in.
    /// </summary>
    public sealed class NullRealtimeTransport : IRealtimeTransport
    {
        public event Action Connected;
        public event Action<string> TextMessageReceived;
        public event Action<byte[]> BinaryMessageReceived;
        public event Action<Exception> Error;
        public event Action Closed;

        public Task ConnectAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            Connected?.Invoke();
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            Closed?.Invoke();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

