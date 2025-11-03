using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DFIN.VoiceAgent.Audio;
using DFIN.VoiceAgent.Configuration;
using DFIN.VoiceAgent.Networking;
using DFIN.VoiceAgent.OpenAI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DFIN.VoiceAgent.ElevenLabs
{
    /// <summary>
    /// MonoBehaviour entry point for connecting to an ElevenLabs conversation agent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MicrophoneCapture))]
    [RequireComponent(typeof(StreamingAudioPlayer))]
    public class ElevenLabsRealtimeController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private VoiceAgentSettings settingsOverride;

        [SerializeField]
        private bool connectOnStart = true;

        [SerializeField]
        private bool logEvents;

        [SerializeField]
        private bool logAudioEvents;

        [SerializeField]
        private bool ensureAudioListener = true;

        [Header("Session Overrides")]
        [SerializeField]
        [Tooltip("Voice id requested for this controller instance. Leave blank to fall back to project defaults / agent configuration.")]
        private string voiceOverride = string.Empty;

        private ElevenLabsRealtimeClient client;
        private ElevenLabsVoiceSettings elevenLabsSettings;
        private IRealtimeTransport transport;
        private MicrophoneCapture microphoneCapture;
        private StreamingAudioPlayer audioPlayer;
        private Pcm16AudioStream audioStream;
        private RealtimeToolRegistry toolRegistry;

        private CancellationTokenSource connectionCts;
        private bool isConnected;
        private int remoteOutputSampleRate;
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

            elevenLabsSettings = settingsAsset.ElevenLabs ?? new ElevenLabsVoiceSettings();
            microphoneCapture = GetComponent<MicrophoneCapture>();
            audioPlayer = GetComponent<StreamingAudioPlayer>();
            remoteOutputSampleRate = Mathf.Max(8000, elevenLabsSettings.outputSampleRate);

            if (string.IsNullOrWhiteSpace(voiceOverride) && !string.IsNullOrWhiteSpace(elevenLabsSettings.voiceId))
            {
                voiceOverride = elevenLabsSettings.voiceId.Trim();
            }

            transport ??= new NativeWebSocketTransport();
            client = new ElevenLabsRealtimeClient(elevenLabsSettings, transport);
            toolRegistry = new RealtimeToolRegistry();
            audioStream = new Pcm16AudioStream();

            client.Connected += HandleConnected;
            client.Closed += HandleClosed;
            client.Error += HandleError;
            client.TextMessageReceived += HandleTextMessage;
            client.BinaryMessageReceived += HandleBinaryMessage;

            microphoneCapture.SampleReady += HandleMicrophoneSamples;
            audioStream.SamplesAvailable += HandleAudioSamplesAvailable;
            audioStream.SegmentCompleted += HandleAudioSegmentCompleted;

            if (ensureAudioListener && FindFirstObjectByType<AudioListener>() == null)
            {
                var listenerObject = new GameObject("VoiceAgentAudioListener");
                listenerObject.AddComponent<AudioListener>();
                listenerObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(listenerObject);
                if (logEvents)
                {
                    Debug.Log("[ElevenLabs] Created fallback AudioListener.", this);
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
                // the client funnels errors through HandleError
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

        public ElevenLabsRealtimeClient Client => client;

        public void SetVoice(string voiceId, bool sendUpdate = true)
        {
            var normalized = string.IsNullOrWhiteSpace(voiceId) ? string.Empty : voiceId.Trim();
            if (string.Equals(voiceOverride, normalized, StringComparison.Ordinal))
            {
                return;
            }

            voiceOverride = normalized;

            if (sendUpdate && isConnected)
            {
                _ = SendConversationInitiationAsync();
            }
        }

        private string GetResolvedVoiceId()
        {
            if (!string.IsNullOrWhiteSpace(voiceOverride))
            {
                return voiceOverride.Trim();
            }

            if (!string.IsNullOrWhiteSpace(elevenLabsSettings.voiceId))
            {
                return elevenLabsSettings.voiceId.Trim();
            }

            return string.Empty;
        }

        private async void HandleConnected()
        {
            Debug.Log("ElevenLabs realtime connected.");
            isConnected = true;
            audioStream?.Reset();

            try
            {
                toolRegistry?.DiscoverSceneTools();
                await SendConversationInitiationAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElevenLabs] Failed to send conversation initiation: {ex.Message}", this);
            }
        }

        private void HandleClosed()
        {
            Debug.Log("ElevenLabs realtime closed.");
            isConnected = false;
            audioStream?.Reset();
        }

        private void HandleError(Exception exception)
        {
            Debug.LogError($"ElevenLabs realtime error: {exception.Message}", this);
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

                if (logEvents)
                {
                    Debug.Log($"[ElevenLabs] {type ?? "event"}: {message}", this);
                }

                UpdateOutputSampleRate(json);

                if (string.Equals(type, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    SendPong(json);
                    return;
                }

                if (string.Equals(type, "client_tool_call", StringComparison.OrdinalIgnoreCase))
                {
                    HandleToolCall(json["client_tool_call"] as JObject);
                    return;
                }

                if (json["client_tool_call"] is JObject nestedToolCall)
                {
                    HandleToolCall(nestedToolCall);
                }

                ProcessAudioPayload(json);
                LogTranscript(json);
            }
            catch (Exception)
            {
                Debug.Log($"[ElevenLabs] {message}", this);
            }
        }

        private void HandleBinaryMessage(byte[] payload)
        {
            // reserved for future binary streaming support
        }

        private async Task SendConversationInitiationAsync()
        {
            if (client == null || !isConnected)
            {
                return;
            }

            var voiceId = GetResolvedVoiceId();
            if (string.IsNullOrEmpty(voiceId))
            {
                return;
            }

            var payload = new JObject
            {
                ["type"] = "conversation_initiation_client_data",
                ["conversation_config_override"] = new JObject
                {
                    ["tts"] = new JObject
                    {
                        ["voice_id"] = voiceId
                    }
                }
            };

            try
            {
                await client.SendTextAsync(payload.ToString(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElevenLabs] Failed to send conversation initiation payload: {ex.Message}", this);
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
                ["user_audio_chunk"] = base64
            };

            _ = client.SendTextAsync(message.ToString(), CancellationToken.None);
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

            audioPlayer?.EnqueuePcm16Samples(samples, remoteOutputSampleRate);

            if (logAudioEvents)
            {
                Debug.Log($"[ElevenLabs] Queued audio segment ({samples.Length} samples @ {remoteOutputSampleRate} Hz)", this);
            }
        }

        private void HandleAudioSegmentCompleted()
        {
            // no-op for now
        }

        private void ProcessAudioPayload(JObject json)
        {
            var audio = ExtractAudioBase64(json);
            if (string.IsNullOrEmpty(audio))
            {
                return;
            }

            audioStream?.AppendDelta(audio);
            if (logAudioEvents)
            {
                Debug.Log($"[ElevenLabs] Appended audio ({audio.Length} chars)", this);
            }

            if (IsResponseComplete(json))
            {
                audioStream?.MarkSegmentComplete();
            }
        }

        private static bool IsResponseComplete(JObject json)
        {
            var completionToken = json.SelectTokens("$..is_final").OfType<JValue>().FirstOrDefault();
            if (completionToken != null && completionToken.Type == JTokenType.Boolean)
            {
                return completionToken.Value<bool>();
            }

            var type = json["type"]?.ToString();
            return string.Equals(type, "agent_response_completed", StringComparison.OrdinalIgnoreCase);
        }

        private void LogTranscript(JObject json)
        {
            if (!logEvents)
            {
                return;
            }

            var transcriptToken = json.SelectTokens("$..user_transcript").FirstOrDefault() ?? json.SelectTokens("$..transcript").FirstOrDefault();
            if (transcriptToken != null && transcriptToken.Type == JTokenType.String)
            {
                var transcript = transcriptToken.ToString();
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    Debug.Log($"[ElevenLabs] Transcript: {transcript}", this);
                }
            }

            var vadToken = json.SelectTokens("$..vad_score").FirstOrDefault() as JValue;
            if (vadToken != null && (vadToken.Type == JTokenType.Float || vadToken.Type == JTokenType.Integer))
            {
                Debug.Log($"[ElevenLabs] VAD score: {vadToken.Value<double>():0.000}", this);
            }
        }

        private void UpdateOutputSampleRate(JObject json)
        {
            var formatToken = json.SelectTokens("$..agent_output_audio_format").FirstOrDefault();
            if (formatToken == null || formatToken.Type != JTokenType.String)
            {
                return;
            }

            var parsed = ParseSampleRate(formatToken.ToString());
            if (parsed > 0 && parsed != remoteOutputSampleRate)
            {
                remoteOutputSampleRate = parsed;
                if (logEvents)
                {
                    Debug.Log($"[ElevenLabs] Agent output sample rate updated to {remoteOutputSampleRate} Hz", this);
                }
            }
        }

        private static int ParseSampleRate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits))
            {
                return 0;
            }

            if (int.TryParse(digits, out var rate))
            {
                return Mathf.Clamp(rate, 8000, 48000);
            }

            return 0;
        }

        private void SendPong(JObject ping)
        {
            if (client == null)
            {
                return;
            }

            var payload = new JObject { ["type"] = "pong" };
            var pingId = ping?["ping_id"] ?? ping?["id"];
            if (pingId != null)
            {
                payload["ping_id"] = pingId;
            }

            _ = client.SendTextAsync(payload.ToString(), CancellationToken.None);
        }

        private void HandleToolCall(JObject payload)
        {
            _ = ProcessToolCallAsync(payload);
        }

        private async Task ProcessToolCallAsync(JObject payload)
        {
            if (payload == null || toolRegistry == null)
            {
                return;
            }

            var callId = payload["tool_call_id"]?.ToString();
            var toolName = payload["tool_name"]?.ToString();
            var parametersToken = payload["parameters"];

            if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(toolName))
            {
                Debug.LogWarning("[ElevenLabs] Received client tool call without call id or tool name.");
                return;
            }

            if (!toolRegistry.TryGetTool(toolName, out var definition))
            {
                toolRegistry.DiscoverSceneTools();
                if (!toolRegistry.TryGetTool(toolName, out definition))
                {
                    Debug.LogWarning($"[ElevenLabs] Unknown tool requested: {toolName}");
                    await SendToolResultAsync(callId, $"Tool '{toolName}' is not available.", true);
                    return;
                }
            }

            JObject arguments = null;
            if (parametersToken != null)
            {
                try
                {
                    if (parametersToken.Type == JTokenType.String)
                    {
                        var text = parametersToken.ToString();
                        arguments = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
                    }
                    else if (parametersToken.Type == JTokenType.Object)
                    {
                        arguments = (JObject)parametersToken;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ElevenLabs] Failed to parse tool arguments for '{toolName}': {ex.Message}");
                    await SendToolResultAsync(callId, $"Failed to parse tool arguments: {ex.Message}", true);
                    return;
                }
            }

            var result = definition.Invoke(arguments, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[ElevenLabs] Tool '{toolName}' error: {error}");
                await SendToolResultAsync(callId, error, true);
                return;
            }

            string output = null;
            if (result is string str)
            {
                output = str;
            }
            else if (result != null)
            {
                try
                {
                    output = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                }
                catch
                {
                    output = result.ToString();
                }
            }

            await SendToolResultAsync(callId, output);
        }

        private async Task SendToolResultAsync(string callId, string result, bool isError = false)
        {
            if (client == null || string.IsNullOrWhiteSpace(callId))
            {
                return;
            }

            var payload = new JObject
            {
                ["type"] = "client_tool_result",
                ["tool_call_id"] = callId,
                ["is_error"] = isError,
                ["result"] = string.IsNullOrWhiteSpace(result) ? "{}" : result
            };

            try
            {
                await client.SendTextAsync(payload.ToString(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElevenLabs] Failed to send tool result: {ex.Message}", this);
            }
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

                if (token is JArray array && array.FirstOrDefault() is JValue valueToken && valueToken.Type == JTokenType.String)
                {
                    var value = valueToken.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }
}
