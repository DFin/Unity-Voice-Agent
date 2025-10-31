# OpenAI Realtime (WebSocket) — Quick Reference
_Last verified: Oct 31, 2025_

This page shows the tiny subset we use in **Realtime Voice AI** to stream mic PCM16 to OpenAI and play back the model’s speech, with optional tool (function) calling.

---

## Unity Dependencies

- **NativeWebSocket** (https://github.com/endel/NativeWebSocket.git#upm) — required because `System.Net.WebSockets` is not available on IL2CPP targets (Android, iOS, Quest). Works across desktop and mobile.
- **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`) — used for parsing nested realtime payloads; maintained by Unity and IL2CPP safe.

Add both via Package Manager before running the OpenAI pipeline.

**Configuration Asset**

- Create `VoiceAgentSettings` via `Voice Agent → Settings`. The asset is stored under `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset` so runtime scripts can load it with `VoiceAgentSettings.Load()`.
- API keys remain in plain text—rotate frequently and avoid checking production secrets into version control.
- `OpenAiRealtimeSettings.modelId` defaults to `gpt-realtime`.
- `OpenAiRealtimeSettings.outputSampleRate` controls how playback clips are created (defaults to 24 kHz to match OpenAI's voices).
- Semantic VAD (server-side turn detection) is enabled by default. You can toggle automatic response creation, eagerness (`auto`, `low`, `medium`, `high`), and interruption behavior from the settings asset.

**Runtime Scaffolding (Phase 1)**

- `NativeWebSocketTransport` wraps the NativeWebSocket package so we can connect on desktop, Android, iOS, Quest, and WebGL.
- `OpenAiRealtimeController` MonoBehaviour loads settings, connects, sends a `session.update`, dispatches NativeWebSocket messages, forwards mic audio as `input_audio_buffer.append`, and handles response audio playback (it also spawns a fallback `AudioListener` when a scene lacks one). It tracks in-flight `response_id`s so it can cancel/clear audio buffers when the user starts speaking again.
- `MicrophoneCapture` publishes raw float sample buffers (16 kHz by default) to feed into the realtime API.
- `OpenAiAudioStream` forwards every `response.output_audio.delta` / `response.audio.delta` payload as PCM16 samples, and `StreamingAudioPlayer` pushes them into a streaming AudioClip so playback begins immediately. Basic linear resampling covers 24 kHz → Unity output rates. You can call `OpenAiRealtimeController.CancelActiveResponses()` to forward `response.cancel` and buffer-clear messages when needed.
- The mic bridge currently auto-streams; server-side VAD will trigger responses. Manual commit / response requests will be added alongside tooling UX.

---

## 1) Connect

**WebSocket URL**

    wss://api.openai.com/v1/realtime?model=gpt-realtime

**Headers**

    Authorization: Bearer <OPENAI_API_KEY>

Tip: If you change the model id, keep it to a realtime‑capable model per the Models page.

---

## 2) Start a session

Send a `session.update` client event to set basics:

    {
      "type": "session.update",
      "session": {
        "type": "realtime",
        "modalities": ["audio","text"],
        "turn_detection": { "type": "server_vad" },
        "audio": {
          "output": { "voice": "alloy" }
        },
        "instructions": "You are a concise assistant."
      }
    }

Notes:
- **Server VAD** lets the API detect speech start/stop.
- Most session fields can be updated at runtime; **voice** should be set before the first audio response.

---

## 3) Stream mic audio (PCM16, 16 kHz)

Convert Unity mic to **mono, 16 kHz, 16‑bit PCM** and base64‑encode each chunk:

    { "type": "input_audio_buffer.append", "audio": "<base64 pcm16@16k>" }

With server VAD enabled, the API commits when it detects end‑of‑speech; you can also explicitly send:

    { "type": "input_audio_buffer.commit" }

---

## 4) Receive model audio

Expect **`response.output_audio.delta`** (older) or **`response.audio.delta`** (newer) events carrying base64 PCM audio, with matching `…done` events. Decode each delta immediately and enqueue it into the streaming buffer; don’t wait for the `done` before playing back.

Optionally, read transcript deltas (`output_audio_transcript.delta`) to show live captions.

---

## 5) Ask for responses explicitly (optional)

You can force a turn by sending:

    {
      "type": "response.create",
      "response": {
        "modalities": ["audio","text"]
      }
    }

---

## 6) Function calling (tools)

Register tool schemas up front, then handle tool calls on the stream:

    {
      "type": "session.update",
      "session": {
        "tools": [
          {
            "type": "function",
            "name": "move_cube",
            "description": "Move the cube by dx,dy,dz meters.",
            "parameters": {
              "type":"object",
              "properties":{
                "dx":{"type":"number"},
                "dy":{"type":"number"},
                "dz":{"type":"number"}
              },
              "required":["dx","dy","dz"]
            }
          }
        ]
      }
    }

The model will emit tool‑call events on responses; your app runs the function and streams back the tool result.

---

## 7) Common server events to expect (non‑exhaustive)

- `session.created` / `session.updated`
- `input_audio_buffer.speech_started` / `speech_stopped` (when using server VAD)
- `response.output_audio.delta` / `response.audio.delta` and their corresponding `…done`
- Tool‑call events on `response.*` (names may evolve; switch on the `type` string and read payload)

---

## 8) Troubleshooting

- 4xx during connect → verify **Authorization** header and model id.
- No audio received → ensure `modalities` include `"audio"` and a **voice** is set before first audio output.
- VAD too eager/slow → tweak `turn_detection` thresholds in `session.update`.
- User speech overlaps assistant audio → the controller now issues `response.cancel`, `output_audio_buffer.clear`, and `input_audio_buffer.clear` when mic RMS spikes; tune the threshold or call `CancelActiveResponses()` manually if needed.
