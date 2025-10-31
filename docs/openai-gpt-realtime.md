# OpenAI Realtime (WebSocket) — Quick Reference
_Last verified: Oct 31, 2025_

This page shows the tiny subset we use in **Realtime Voice AI** to stream mic PCM16 to OpenAI and play back the model’s speech, with optional tool (function) calling.

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

Expect **`response.output_audio.delta`** events carrying base64 PCM audio, and a matching `...done` when the segment finishes.

Write each delta to a ring buffer; resample to Unity’s output rate for playback.

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
- `response.output_audio.delta` / `response.output_audio.done`
- Tool‑call events on `response.*` (names may evolve; switch on the `type` string and read payload)

---

## 8) Troubleshooting

- 4xx during connect → verify **Authorization** header and model id.
- No audio received → ensure `modalities` include `"audio"` and a **voice** is set before first audio output.
- VAD too eager/slow → tweak `turn_detection` thresholds in `session.update`.