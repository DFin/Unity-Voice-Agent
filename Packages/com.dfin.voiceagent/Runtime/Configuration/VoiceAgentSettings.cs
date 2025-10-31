using System;
using UnityEngine;

namespace DFIN.VoiceAgent.Configuration
{
    /// <summary>
    /// Central configuration asset for all voice-agent services. Stored in Resources for runtime access.
    /// </summary>
    public class VoiceAgentSettings : ScriptableObject
    {
        public const string ResourcesLoadPath = "VoiceAgentSettings";

#if UNITY_EDITOR
        public const string ResourcesFolder = "Assets/VoiceAgent/Resources";
        public const string AssetFileName = "VoiceAgentSettings.asset";
        public static string AssetPath => $"{ResourcesFolder}/{AssetFileName}";
#endif

        [SerializeField]
        private OpenAiRealtimeSettings openAi = new();

        [SerializeField]
        private ElevenLabsVoiceSettings elevenLabs = new();

        public OpenAiRealtimeSettings OpenAi => openAi;
        public ElevenLabsVoiceSettings ElevenLabs => elevenLabs;

        /// <summary>
        /// Attempts to load the configuration from Resources. Returns null if none found.
        /// </summary>
        public static VoiceAgentSettings Load()
        {
            return Resources.Load<VoiceAgentSettings>(ResourcesLoadPath);
        }
    }

    [Serializable]
    public class OpenAiRealtimeSettings
    {
        [Tooltip("Bearer token used when connecting to the OpenAI Realtime WebSocket. Stored in plain text; rotate regularly.")]
        public string apiKey = string.Empty;

        [Tooltip("Realtime-capable model identifier.")]
        public string modelId = "gpt-realtime";

        [Tooltip("Base WebSocket URL used for realtime sessions.")]
        public string endpointUrl = "wss://api.openai.com/v1/realtime";

        [Tooltip("Default instructions sent on session.start/session.update.")]
        [TextArea(2, 5)]
        public string systemInstructions = "You are a helpful teaching assistant. Keep answers short and clear.";

        [Tooltip("Requested output voice id (OpenAI voice library).")]
        public string voice = "alloy";

        [Tooltip("Enable server-side voice activity detection.")]
        public bool serverVadEnabled = true;

        [Tooltip("Settings for semantic voice activity detection when enabled.")]
        public SemanticVadSettings semanticVad = new();

        [Tooltip("Expected sample rate of audio returned by the realtime API (Hz).")]
        public int outputSampleRate = 24000;
    }

    [Serializable]
    public class ElevenLabsVoiceSettings
    {
        [Tooltip("API key for ElevenLabs streaming voice. Currently unused; planned for Phase 2.")]
        public string apiKey = string.Empty;

        [Tooltip("Default voice id to request for ElevenLabs playback.")]
        public string voiceId = string.Empty;
    }

    [Serializable]
    public class SemanticVadSettings
    {
        [Tooltip("Automatically create a response when speech stops.")]
        public bool createResponse = true;

        [Tooltip("How quickly the assistant should respond once speech stops.")]
        public VadEagerness eagerness = VadEagerness.Auto;

        [Tooltip("Interrupt the current assistant response when new speech starts.")]
        public bool interruptResponse = true;
    }

    public enum VadEagerness
    {
        Auto,
        Low,
        Medium,
        High
    }
}
