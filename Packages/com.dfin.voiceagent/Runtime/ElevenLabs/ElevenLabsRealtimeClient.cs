using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DFIN.VoiceAgent.Configuration;
using DFIN.VoiceAgent.Networking;
using UnityEngine;

namespace DFIN.VoiceAgent.ElevenLabs
{
    /// <summary>
    /// Lightweight wrapper for connecting to the ElevenLabs conversation WebSocket.
    /// </summary>
    public class ElevenLabsRealtimeClient : IDisposable
    {
        public event Action Connected;
        public event Action<string> TextMessageReceived;
        public event Action<byte[]> BinaryMessageReceived;
        public event Action Closed;
        public event Action<Exception> Error;

        private readonly ElevenLabsVoiceSettings settings;
        private readonly IRealtimeTransport transport;
        private readonly Action transportConnectedHandler;
        private readonly Action<string> transportTextHandler;
        private readonly Action<byte[]> transportBinaryHandler;
        private readonly Action transportClosedHandler;
        private readonly Action<Exception> transportErrorHandler;

        private bool isConnecting;
        private bool disposed;

        public ElevenLabsRealtimeClient(ElevenLabsVoiceSettings settings, IRealtimeTransport transport)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));

            transportConnectedHandler = () => Connected?.Invoke();
            transportTextHandler = message => TextMessageReceived?.Invoke(message);
            transportBinaryHandler = payload => BinaryMessageReceived?.Invoke(payload);
            transportClosedHandler = () => Closed?.Invoke();
            transportErrorHandler = exception => Error?.Invoke(exception);

            HookTransportEvents();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (isConnecting)
            {
                Debug.LogWarning("ElevenLabs realtime client is already connecting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.apiKey) && string.IsNullOrWhiteSpace(settings.conversationUrlOverride))
            {
                throw new InvalidOperationException("ElevenLabs API key is missing. Populate it via Voice Agent → Settings (or supply a signed conversation URL override).");
            }

            var uri = BuildConversationUri();
            var headers = BuildHeaders();

            isConnecting = true;
            try
            {
                await transport.ConnectAsync(uri, headers, cancellationToken);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                throw;
            }
            finally
            {
                isConnecting = false;
            }
        }

        public Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return transport.SendTextAsync(message, cancellationToken);
        }

        public Task SendBinaryAsync(ArraySegment<byte> payload, CancellationToken cancellationToken)
        {
            return transport.SendBinaryAsync(payload, cancellationToken);
        }

        public Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            return transport.CloseAsync(reason, cancellationToken);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            UnhookTransportEvents();
            transport.Dispose();
            disposed = true;
        }

        private Uri BuildConversationUri()
        {
            var overrideUrl = settings.conversationUrlOverride?.Trim();
            if (!string.IsNullOrEmpty(overrideUrl))
            {
                return new Uri(overrideUrl);
            }

            var endpoint = settings.endpointUrl?.Trim();
            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = "wss://api.elevenlabs.io/v1/convai/conversation";
            }

            var agentId = settings.agentId?.Trim();
            if (string.IsNullOrEmpty(agentId))
            {
                throw new InvalidOperationException("ElevenLabs agent id is missing. Populate it via Voice Agent → Settings.");
            }

            var separator = endpoint.Contains("?") ? "&" : "?";
            var uriString = $"{endpoint}{separator}agent_id={Uri.EscapeDataString(agentId)}";
            return new Uri(uriString);
        }

        private IReadOnlyDictionary<string, string> BuildHeaders()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(settings.apiKey))
            {
                headers["xi-api-key"] = settings.apiKey.Trim();
            }

            return headers;
        }

        private void HookTransportEvents()
        {
            transport.Connected += transportConnectedHandler;
            transport.TextMessageReceived += transportTextHandler;
            transport.BinaryMessageReceived += transportBinaryHandler;
            transport.Closed += transportClosedHandler;
            transport.Error += transportErrorHandler;
        }

        private void UnhookTransportEvents()
        {
            transport.Connected -= transportConnectedHandler;
            transport.TextMessageReceived -= transportTextHandler;
            transport.BinaryMessageReceived -= transportBinaryHandler;
            transport.Closed -= transportClosedHandler;
            transport.Error -= transportErrorHandler;
        }
    }
}
