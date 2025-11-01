using System;
using System.Collections.Generic;
using System.Linq;
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
    [RequireComponent(typeof(StreamingAudioPlayer))]
    public class OpenAiRealtimeController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private VoiceAgentSettings settingsOverride;

        [SerializeField]
        private bool connectOnStart = true;

        [SerializeField]
        private bool logAudioEvents;

        [SerializeField]
        private bool ensureAudioListener = true;

        [Header("Session Overrides")]
        private const string DefaultInstructions = "You are a helpful teaching assistant. Keep answers short and clear.";

        [SerializeField, TextArea(2, 5)]
        [Tooltip("Overrides the VoiceAgentSettings system instructions just for this controller instance.")]
        private string sessionInstructionsOverride = DefaultInstructions;

        private OpenAiRealtimeClient client;
        private IRealtimeTransport transport;
        private MicrophoneCapture microphoneCapture;
        private StreamingAudioPlayer audioPlayer;
        private OpenAiAudioStream audioStream;
        private readonly HashSet<string> activeResponses = new();

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
            audioPlayer = GetComponent<StreamingAudioPlayer>();

            transport ??= new NativeWebSocketTransport();
            client = new OpenAiRealtimeClient(openAiSettings, transport);

            client.Connected += HandleConnected;
            client.Closed += HandleClosed;
            client.Error += HandleError;
            client.TextMessageReceived += HandleTextMessage;
            client.BinaryMessageReceived += HandleBinaryMessage;

            microphoneCapture.SampleReady += HandleMicrophoneSamples;
            audioStream = new OpenAiAudioStream();
            audioStream.SamplesAvailable += HandleAudioSamplesAvailable;
            audioStream.SegmentCompleted += HandleAudioSegmentCompleted;

            if (ensureAudioListener && FindFirstObjectByType<AudioListener>() == null)
            {
                var listenerObject = new GameObject("VoiceAgentAudioListener");
                listenerObject.AddComponent<AudioListener>();
                listenerObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(listenerObject);
                if (logAudioEvents)
                {
                    Debug.Log("[OpenAI Realtime] Created fallback AudioListener.", this);
                }
            }
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
                client.BinaryMessageReceived -= HandleBinaryMessage;
                client.Dispose();
                client = null;
            }

            if (microphoneCapture != null)
            {
                microphoneCapture.SampleReady -= HandleMicrophoneSamples;
            }

            if (audioStream != null)
            {
                audioStream.SamplesAvailable -= HandleAudioSamplesAvailable;
                audioStream.SegmentCompleted -= HandleAudioSegmentCompleted;
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

        public void SetSystemInstructions(string instructions, bool sendUpdate = true)
        {
            var normalized = string.IsNullOrWhiteSpace(instructions) ? string.Empty : instructions.Trim();
            if (string.Equals(sessionInstructionsOverride, normalized, StringComparison.Ordinal))
            {
                return;
            }

            sessionInstructionsOverride = normalized;

            if (sendUpdate && isConnected)
            {
                _ = SendSessionUpdateAsync();
            }
        }

        private string GetResolvedSystemInstructions()
        {
            if (!string.IsNullOrWhiteSpace(sessionInstructionsOverride))
            {
                return sessionInstructionsOverride.Trim();
            }

            return DefaultInstructions;
        }

        private void HandleConnected()
        {
            Debug.Log("OpenAI realtime connected.");
            isConnected = true;
            activeResponses.Clear();
            _ = SendSessionUpdateAsync();
            audioStream?.Reset();
        }

        private void HandleClosed()
        {
            Debug.Log("OpenAI realtime closed.");
            isConnected = false;
            activeResponses.Clear();
            audioStream?.Reset();
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
                var responseId = json["response_id"]?.ToString() ?? json["response"]?["id"]?.ToString();
                Debug.Log($"[OpenAI Realtime] {type ?? "event"}: {message}", this);

                switch (type)
                {
                    case "response.output_audio.delta":
                    {
                        var audio = ExtractAudioBase64(json);
                        if (!string.IsNullOrEmpty(audio))
                        {
                            audioStream?.AppendDelta(audio);
                            if (logAudioEvents)
                            {
                                Debug.Log($"[OpenAI Realtime] Appended audio delta ({audio.Length} chars)", this);
                            }
                        }
                        RegisterActiveResponse(responseId);
                        break;
                    }
                    case "response.audio.delta":
                    {
                        var audio = json["delta"]?.ToString();
                        if (!string.IsNullOrEmpty(audio))
                        {
                            audioStream?.AppendDelta(audio);
                            if (logAudioEvents)
                            {
                                Debug.Log($"[OpenAI Realtime] Appended audio delta ({audio.Length} chars)", this);
                            }
                        }
                        RegisterActiveResponse(responseId);
                        break;
                    }
                    case "response.output_audio.done":
                    case "response.audio.done":
                        if (logAudioEvents)
                        {
                            Debug.Log("[OpenAI Realtime] Audio segment done", this);
                        }
                        audioStream?.MarkSegmentComplete();
                        if (!string.IsNullOrEmpty(responseId))
                        {
                            activeResponses.Remove(responseId);
                        }
                        break;
                    case "response.created":
                        RegisterActiveResponse(json["response"]?["id"]?.ToString());
                        break;
                    case "response.done":
                        if (!string.IsNullOrEmpty(responseId))
                        {
                            activeResponses.Remove(responseId);
                        }

                        var status = json["response"]?["status"]?.ToString();
                        if (!string.IsNullOrEmpty(status) && string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            var reason = json["response"]?["status_details"]?["reason"]?.ToString();
                            if (string.Equals(reason, "turn_detected", StringComparison.OrdinalIgnoreCase))
                            {
                                audioStream?.MarkSegmentComplete();
                                audioPlayer?.Clear();
                            }
                        }
                        break;
                    case "input_audio_buffer.speech_started":
                        if (logAudioEvents)
                        {
                            Debug.Log("[OpenAI Realtime] input_audio_buffer.speech_started", this);
                        }
                        audioStream?.MarkSegmentComplete();
                        audioPlayer?.Clear();
                        break;
                }
            }
            catch (Exception)
            {
                Debug.Log($"[OpenAI Realtime] {message}", this);
            }
        }

        private void HandleBinaryMessage(byte[] payload)
        {
            // Currently unused; realtime API sends text events. Reserved for future binary streaming support.
        }

        private async Task SendSessionUpdateAsync()
        {
            if (client == null || openAiSettings == null || !isConnected)
            {
                return;
            }

            var session = new JObject
            {
                ["modalities"] = new JArray("audio", "text")
            };

            if (!string.IsNullOrWhiteSpace(openAiSettings.voice))
            {
                session["voice"] = openAiSettings.voice;
            }

            var instructions = GetResolvedSystemInstructions();
            if (!string.IsNullOrEmpty(instructions))
            {
                session["instructions"] = instructions;
            }

            ApplyTurnDetection(session);

            var payload = new JObject
            {
                ["type"] = "session.update",
                ["session"] = session
            };

            try
            {
                await client.SendTextAsync(payload.ToString(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send session.update: {ex.Message}", this);
            }
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

        private void ApplyTurnDetection(JObject session)
        {
            if (session == null || openAiSettings == null)
            {
                return;
            }

            var turnSettings = openAiSettings.turnDetection ?? new TurnDetectionSettings();
            switch (turnSettings.mode)
            {
                case TurnDetectionMode.None:
                    session["turn_detection"] = JValue.CreateNull();
                    break;

                case TurnDetectionMode.ServerVad:
                {
                    var server = turnSettings.server ?? new ServerVadSettings();
                    var turnDetection = new JObject
                    {
                        ["type"] = "server_vad",
                        ["create_response"] = server.createResponse,
                        ["interrupt_response"] = server.interruptResponse,
                        ["threshold"] = server.threshold,
                        ["prefix_padding_ms"] = server.prefixPaddingMs,
                        ["silence_duration_ms"] = server.silenceDurationMs
                    };

                    if (server.idleTimeoutMs > 0)
                    {
                        turnDetection["idle_timeout_ms"] = server.idleTimeoutMs;
                    }

                    session["turn_detection"] = turnDetection;
                    break;
                }

                case TurnDetectionMode.SemanticVad:
                default:
                {
                    var semantic = turnSettings.semantic ?? new SemanticVadSettings();
                    var turnDetection = new JObject
                    {
                        ["type"] = "semantic_vad",
                        ["create_response"] = semantic.createResponse,
                        ["interrupt_response"] = semantic.interruptResponse,
                        ["eagerness"] = MapSemanticEagerness(semantic.eagerness)
                    };

                    session["turn_detection"] = turnDetection;
                    break;
                }
            }
        }

        private void HandleAudioSamplesAvailable(short[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            if (audioPlayer == null)
            {
                audioPlayer = GetComponent<StreamingAudioPlayer>();
            }

            var outputSampleRate = openAiSettings != null ? Mathf.Max(8000, openAiSettings.outputSampleRate) : 24000;
            audioPlayer?.EnqueuePcm16Samples(samples, outputSampleRate);

            if (logAudioEvents)
            {
                Debug.Log($"[OpenAI Realtime] Queued audio segment ({samples.Length} samples @ {outputSampleRate} Hz)", this);
            }
        }

        private void HandleAudioSegmentCompleted()
        {
            // currently no-op, placeholder for future callbacks
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

        private static string ExtractAudioBase64(JObject json)
        {
            foreach (var token in json.SelectTokens("$..audio"))
            {
                if (token.Type == JTokenType.String)
                {
                    var value = token.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private static string MapSemanticEagerness(VadEagerness eagerness)
        {
            switch (eagerness)
            {
                case VadEagerness.Low:
                    return "low";
                case VadEagerness.Medium:
                    return "medium";
                case VadEagerness.High:
                    return "high";
                case VadEagerness.Auto:
                default:
                    return "auto";
            }
        }

        private void RegisterActiveResponse(string responseId)
        {
            if (!string.IsNullOrEmpty(responseId))
            {
                activeResponses.Add(responseId);
            }
        }

        public void CancelActiveResponses()
        {
            if (client == null)
            {
                return;
            }

            var ids = activeResponses.Count > 0 ? activeResponses.ToArray() : Array.Empty<string>();
            if (ids.Length == 0)
            {
                SendEvent(new JObject { ["type"] = "response.cancel" });
            }
            else
            {
                foreach (var id in ids)
                {
                    SendEvent(new JObject
                    {
                        ["type"] = "response.cancel",
                        ["response_id"] = id
                    });
                }
            }

            SendEvent(new JObject { ["type"] = "output_audio_buffer.clear" });
            SendEvent(new JObject { ["type"] = "input_audio_buffer.clear" });
            audioPlayer?.Clear();
            activeResponses.Clear();
        }

        private void SendEvent(JObject payload)
        {
            if (payload == null || client == null)
            {
                return;
            }

            _ = client.SendTextAsync(payload.ToString(), CancellationToken.None);
        }
    }
}
