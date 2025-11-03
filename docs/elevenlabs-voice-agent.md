# ElevenLabs Agents — WebSocket Quick Reference
_Last verified: Oct 31, 2025_

This page shows the minimal messages we use to talk to an **Agent** over WebSocket, stream mic PCM16 to it, and play its audio replies. It also covers the built‑in **tool calling** round‑trip.

---

## 1) Connect

**WebSocket URL**

    wss://api.elevenlabs.io/v1/convai/conversation?agent_id=<AGENT_ID>

- Public agents can use the URL directly. Private agents typically use a signed URL from your server. For student projects, a public agent ID is the simplest path.

---

## 2) (Optional) Override voice at start

Right after connecting, send **conversation initiation** data to override TTS voice, first message, etc.:

    {
      "type": "conversation_initiation_client_data",
      "conversation_config_override": {
        "tts": { "voice_id": "21m00Tcm4TlvDq8ikWAM" }
      }
    }

You will receive conversation metadata indicating formats, e.g. `agent_output_audio_format: "pcm_16000"` and `user_input_audio_format: "pcm_16000"`.

---

## 3) Stream mic audio (PCM16, 16 kHz)

Send chunks as:

    { "user_audio_chunk": "<base64 pcm16@16k>" }

The platform performs VAD/transcription and will send you:
- `vad_score`
- `user_transcript`
- `agent_response`
- `audio` (base64 audio frames to play)

---

## 4) Function calling (tools)

When the agent needs external data/action, you’ll receive:

    {
      "type": "client_tool_call",
      "client_tool_call": {
        "tool_name": "move_cube",
        "tool_call_id": "tool_call_123",
        "parameters": { "dx": 1.0, "dy": 0.0, "dz": 0.0 }
      }
    }

Run your Unity/C# handler, then **reply**:

    {
      "type": "client_tool_result",
      "tool_call_id": "tool_call_123",
      "result": "{\"status\":\"ok\"}",
      "is_error": false
    }

---

## 5) Ping/Pong

The server may send `ping` with `ping_ms`; reply with `pong` to keep the session healthy.

---

## Unity Integration

- `VoiceAgentSettings.ElevenLabs` now stores the API key, agent id, optional voice override, default endpoint, and expected output sample rate. Create or edit the asset via **Voice Agent → Settings** in the editor.
- Add `ElevenLabsRealtimeController` to a GameObject that also has `MicrophoneCapture` and `StreamingAudioPlayer`. Enable **Connect On Start** to establish the socket automatically during `Start()`.
- The controller wraps `ElevenLabsRealtimeClient` (for WebSocket + `xi-api-key` auth) and reuses `Pcm16AudioStream` + `StreamingAudioPlayer` to decode PCM16 frames and enqueue them for playback.
- Set **Voice Override** on the controller (or in project settings) to send a `conversation_initiation_client_data` payload immediately after connect.
- Console logging is opt-in via the controller’s **Log Events** / **Log Audio Events** toggles so students can inspect `vad_score`, transcripts, tool calls, and ping/pong flow while iterating.
- Tool calls use the same `[RealtimeTool]` attributes as the OpenAI pipeline. When the agent emits `client_tool_call`, the controller looks up the registered method and replies with `client_tool_result` automatically.
