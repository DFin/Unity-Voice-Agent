using System;
using System.Threading;
using System.Threading.Tasks;
using DFIN.VoiceAgent.Configuration;
using DFIN.VoiceAgent.Networking;
using DFIN.VoiceAgent.Audio;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// MonoBehaviour entry point for connecting to the OpenAI realtime API.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MicrophoneCapture))]
    public class OpenAiRealtimeController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private VoiceAgentSettings settingsOverride;

        [SerializeField]
        private bool connectOnStart = true;

        private OpenAiRealtimeClient client;
        private IRealtimeTransport transport;
        private MicrophoneCapture microphoneCapture;

        private CancellationTokenSource connectionCts;
        private OpenAiRealtimeSettings openAiSettings;

        private bool isConnected;
        private short[] pcmBuffer;
        private byte[] byteBuffer;

        private void Awake()
        {
            var settingsAsset = settingsOverride != null ? settingsOverride : VoiceAgentSettings.Load();
            if (settingsAsset == null)
            {
                Debug.LogError("VoiceAgentSettings asset not found. Create one via Voice Agent → Settings.", this);
                enabled = false;
                return;
            }

            openAiSettings = settingsAsset.OpenAi;
            microphoneCapture = GetComponent<MicrophoneCapture>();

            transport ??= new NativeWebSocketTransport();
            client = new OpenAiRealtimeClient(openAiSettings, transport);

            client.Connected += HandleConnected;
            client.Closed += HandleClosed;
            client.Error += HandleError;
            client.TextMessageReceived += HandleTextMessage;

            microphoneCapture.SampleReady += HandleMicrophoneSamples;
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

        private void Update()
        {
            if (transport is NativeWebSocketTransport nativeTransport)
            {
                nativeTransport.DispatchMessageQueue();
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

            if (microphoneCapture != null)
            {
                microphoneCapture.SampleReady -= HandleMicrophoneSamples;
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
            isConnected = true;
            SendSessionUpdate();
        }

        private void HandleClosed()
        {
            Debug.Log("OpenAI realtime closed.");
            isConnected = false;
        }

        private void HandleError(System.Exception exception)
        {
            Debug.LogError($"OpenAI realtime error: {exception.Message}", this);
        }

        private void HandleTextMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                var json = JObject.Parse(message);
                var type = json["type"]?.ToString();
                Debug.Log($"[OpenAI Realtime] {type ?? "event"}: {message}", this);
            }
            catch (Exception)
            {
                Debug.Log($"[OpenAI Realtime] {message}", this);
            }
        }

        private async void SendSessionUpdate()
        {
            if (client == null || openAiSettings == null)
            {
                return;
            }

            var session = new JObject
            {
                ["type"] = "realtime",
                ["modalities"] = new JArray("audio", "text"),
                ["audio"] = new JObject
                {
                    ["output"] = new JObject
                    {
                        ["voice"] = openAiSettings.voice
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(openAiSettings.systemInstructions))
            {
                session["instructions"] = openAiSettings.systemInstructions;
            }

            if (openAiSettings.serverVadEnabled)
            {
                session["turn_detection"] = new JObject
                {
                    ["type"] = "server_vad"
                };
            }

            var payload = new JObject
            {
                ["type"] = "session.update",
                ["session"] = session
            };

            await client.SendTextAsync(payload.ToString(), CancellationToken.None);
        }

        private void HandleMicrophoneSamples(float[] samples)
        {
            if (!isConnected || samples == null || samples.Length == 0)
            {
                return;
            }

            EnsureBuffers(samples.Length);
            ConvertToPcm16(samples, pcmBuffer);
            Buffer.BlockCopy(pcmBuffer, 0, byteBuffer, 0, pcmBuffer.Length * sizeof(short));

            var base64 = Convert.ToBase64String(byteBuffer, 0, pcmBuffer.Length * sizeof(short));
            var message = new JObject
            {
                ["type"] = "input_audio_buffer.append",
                ["audio"] = base64
            };

            _ = client.SendTextAsync(message.ToString(), CancellationToken.None);
        }

        private void EnsureBuffers(int sampleCount)
        {
            if (pcmBuffer == null || pcmBuffer.Length != sampleCount)
            {
                pcmBuffer = new short[sampleCount];
            }

            var byteCount = sampleCount * sizeof(short);
            if (byteBuffer == null || byteBuffer.Length != byteCount)
            {
                byteBuffer = new byte[byteCount];
            }
        }

        private static void ConvertToPcm16(float[] source, short[] destination)
        {
            for (var i = 0; i < source.Length; i++)
            {
                var clamped = Mathf.Clamp(source[i], -1f, 1f);
                destination[i] = (short)Mathf.FloorToInt(clamped * short.MaxValue);
            }
        }
    }
}
