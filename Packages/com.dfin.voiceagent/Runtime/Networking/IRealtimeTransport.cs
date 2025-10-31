using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DFIN.VoiceAgent.Networking
{
    /// <summary>
    /// Abstracts WebSocket functionality so we can swap transport implementations (NativeWebSocket, mock transports, etc.).
    /// </summary>
    public interface IRealtimeTransport : IDisposable
    {
        event Action Connected;
        event Action<string> TextMessageReceived;
        event Action<byte[]> BinaryMessageReceived;
        event Action<Exception> Error;
        event Action Closed;

        Task ConnectAsync(Uri uri, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken);
        Task SendTextAsync(string message, CancellationToken cancellationToken);
        Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken cancellationToken);
        Task CloseAsync(string reason, CancellationToken cancellationToken);
    }
}

