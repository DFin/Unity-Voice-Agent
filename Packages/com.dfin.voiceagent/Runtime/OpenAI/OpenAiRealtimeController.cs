using System.Threading;
using DFIN.VoiceAgent.Configuration;
using DFIN.VoiceAgent.Networking;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// MonoBehaviour entry point for connecting to the OpenAI realtime API.
    /// </summary>
    [DisallowMultipleComponent]
    public class OpenAiRealtimeController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private VoiceAgentSettings settingsOverride;

        [SerializeField]
        private bool connectOnStart = true;

        private OpenAiRealtimeClient client;
        private IRealtimeTransport transport;

        private CancellationTokenSource connectionCts;

        private void Awake()
        {
            var settingsAsset = settingsOverride != null ? settingsOverride : VoiceAgentSettings.Load();
            if (settingsAsset == null)
            {
                Debug.LogError("VoiceAgentSettings asset not found. Create one via Voice Agent → Settings.", this);
                enabled = false;
                return;
            }

            transport ??= new NullRealtimeTransport();
            client = new OpenAiRealtimeClient(settingsAsset.OpenAi, transport);

            client.Connected += HandleConnected;
            client.Closed += HandleClosed;
            client.Error += HandleError;
            client.TextMessageReceived += HandleTextMessage;
        }

        private async void Start()
        {
            if (!connectOnStart || client == null)
            {
                return;
            }

            connectionCts = new CancellationTokenSource();
            try
            {
                await client.ConnectAsync(connectionCts.Token);
            }
            catch
            {
                // The client already reports errors via HandleError.
            }
        }

        private void OnDestroy()
        {
            connectionCts?.Cancel();
            connectionCts?.Dispose();
            connectionCts = null;

            if (client != null)
            {
                client.Connected -= HandleConnected;
                client.Closed -= HandleClosed;
                client.Error -= HandleError;
                client.TextMessageReceived -= HandleTextMessage;
                client.Dispose();
                client = null;
            }

            transport = null;
        }

        public void SetTransport(IRealtimeTransport realtimeTransport)
        {
            if (client != null)
            {
                Debug.LogWarning("Transport override must be supplied before Awake is called.", this);
                return;
            }

            transport = realtimeTransport;
        }

        public OpenAiRealtimeClient Client => client;

        private void HandleConnected()
        {
            Debug.Log("OpenAI realtime connected.");
        }

        private void HandleClosed()
        {
            Debug.Log("OpenAI realtime closed.");
        }

        private void HandleError(System.Exception exception)
        {
            Debug.LogError($"OpenAI realtime error: {exception.Message}", this);
        }

        private void HandleTextMessage(string message)
        {
            Debug.Log($"[OpenAI Realtime] {message}");
        }
    }
}
