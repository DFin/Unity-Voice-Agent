# Development Notes (WIP)

This document will outline the contributor workflow as the project grows. Initial expectations:

- Keep runtime code under `Packages/com.dfin.voiceagent/Runtime/`, editor tooling under `.../Editor/`, and samples under `.../Samples~/`.
- Prefer small, composable components that students can inspect and override.
- Document any new subsystem in `Packages/com.dfin.voiceagent/Documentation~/` alongside code updates.
- Use the existing Unity project for rapid play mode testing; ensure the package remains installable in isolation.
- Configuration assets live in `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset`. Use `Voice Agent → Settings` to create/update it; remember the API keys are stored in plain text for dev only.
- Runtime loop pieces:
  - `NativeWebSocketTransport` implements the cross-platform WebSocket layer.
  - `OpenAiRealtimeController` loads config, connects, sends `session.update`, streams microphone audio, logs events, drives playback, spawns a fallback `AudioListener` if a scene is missing one, and exposes `CancelActiveResponses()` to forward `response.cancel` / buffer-clear messages on demand.
  - `MicrophoneCapture` emits float buffers (16 kHz) consumed by the realtime controller.
  - `OpenAiAudioStream` forwards realtime audio deltas and `StreamingAudioPlayer` writes them into a streaming AudioClip (with 24 kHz → output resampling and optional spatial playback).

Additional guidelines (style rules, testing strategy, PR template) will be added once core systems land.
