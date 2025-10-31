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
        public string modelId = "gpt-4o-realtime-preview";

        [Tooltip("Base WebSocket URL used for realtime sessions.")]
        public string endpointUrl = "wss://api.openai.com/v1/realtime";

        [Tooltip("Default instructions sent on session.start/session.update.")]
        [TextArea(2, 5)]
        public string systemInstructions = "You are a helpful teaching assistant. Keep answers short and clear.";

        [Tooltip("Requested output voice id (OpenAI voice library).")]
        public string voice = "alloy";

        [Tooltip("Enable server-side voice activity detection.")]
        public bool serverVadEnabled = true;
    }

    [Serializable]
    public class ElevenLabsVoiceSettings
    {
        [Tooltip("API key for ElevenLabs streaming voice. Currently unused; planned for Phase 2.")]
        public string apiKey = string.Empty;

        [Tooltip("Default voice id to request for ElevenLabs playback.")]
        public string voiceId = string.Empty;
    }
}

