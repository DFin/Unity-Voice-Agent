# Voice Agent Starter (Overview)

This documentation folder will contain student-facing guides once the first playable loop is live. Planned sections:

- Quick start: configure API keys, press play, and speak to the agent.
- Concepts: how the WebSocket pipeline routes microphone input to OpenAI GPT Realtime and back.
- Customization: swapping voices, enabling spatial audio, and hooking up function calls.
- Troubleshooting: authentication, microphone access, network hiccups.

Current progress:
- VoiceAgentSettings ScriptableObject + editor window (stores OpenAI/ElevenLabs keys).
- NativeWebSocket transport + OpenAI realtime controller (connects, issues initial session update, relays mic audio, logs server events).
- Microphone capture and streaming audio player scaffolding; PCM playback pipeline still TODO.

For now, follow the root `plan.md` for engineering progress and `README.md` for repository usage.
