using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DFIN.VoiceAgent.Configuration;
using DFIN.VoiceAgent.Networking;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// High-level wrapper for connecting to the OpenAI Realtime WebSocket API.
    /// </summary>
    public class OpenAiRealtimeClient : IDisposable
    {
        public event Action Connected;
        public event Action<string> TextMessageReceived;
        public event Action<byte[]> BinaryMessageReceived;
        public event Action Closed;
        public event Action<Exception> Error;

        private readonly OpenAiRealtimeSettings settings;
        private readonly IRealtimeTransport transport;
        private readonly Action transportConnectedHandler;
        private readonly Action<string> transportTextHandler;
        private readonly Action<byte[]> transportBinaryHandler;
        private readonly Action transportClosedHandler;
        private readonly Action<Exception> transportErrorHandler;

        private bool isConnecting;
        private bool disposed;

        public OpenAiRealtimeClient(OpenAiRealtimeSettings settings, IRealtimeTransport transport)
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
                Debug.LogWarning("OpenAI realtime client is already connecting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing. Populate it via Voice Agent → Settings.");
            }

            var uri = BuildRealtimeUri();
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

        private Uri BuildRealtimeUri()
        {
            var endpoint = settings.endpointUrl?.Trim();
            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = "wss://api.openai.com/v1/realtime";
            }

            if (!endpoint.Contains("model=", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = $"{endpoint}?model={settings.modelId}";
            }

            return new Uri(endpoint);
        }

        private IReadOnlyDictionary<string, string> BuildHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {settings.apiKey}",
                ["OpenAI-Beta"] = "realtime=v1"
            };
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
