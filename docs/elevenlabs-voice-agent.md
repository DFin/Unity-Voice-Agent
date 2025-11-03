# ElevenLabs Agents — WebSocket Quick Reference
_Last verified: Oct 31, 2025_

This page shows the minimal messages we use to talk to an **Agent** over WebSocket, stream mic PCM16 to it, and play its audio replies. It also covers the built‑in **tool calling** round‑trip.

---

## 1) Connect

**WebSocket URL**

    wss://api.elevenlabs.io/v1/convai/conversation?agent_id=<AGENT_ID>

- Public agents can use the URL directly. Private agents typically use a signed URL from your server. For student projects, a public agent ID is the simplest path.

### Private agents: request a signed conversation URL

ElevenLabs expects private agents to connect through a **signed URL** that expires quickly. The official JavaScript sample ships a tiny Express backend that exchanges your `xi-api-key` for a signed conversation URL:

```
GET https://api.elevenlabs.io/v1/convai/conversation/get_signed_url?agent_id=<AGENT_ID>
xi-api-key: <XI_API_KEY>
```

The response contains at least `{"signed_url": "wss://..."}` (additional metadata may be included). Your game/server forwards that URL to the Unity client, which then connects with the plain WebSocket constructor.

- Environment variables used by the sample: `XI_API_KEY`, `AGENT_ID`, and optional `PORT`.
- If you expose a helper endpoint (e.g. `/api/signed-url`), keep it server-side only—SDKs are expected to fetch the signed URL moments before connecting.
- Public/education agents can still use the direct `agent_id` URL without signing.

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

The server may send `ping` events, often shaped like:

```
{
  "type": "ping",
  "ping_event": {
    "event_id": 2,
    "ping_ms": null
  }
}
```

Reply with `pong` while echoing back the identifier:

```
{
  "type": "pong",
  "event_id": 2
}
```

Including the original `ping_ms` (when provided) is harmless. Failing to bounce back the `event_id` leads to the server closing the socket.

---

## Unity Integration

- `VoiceAgentSettings.ElevenLabs` now stores the API key, agent id, optional voice override, default endpoint, and expected output sample rate. Create or edit the asset via **Voice Agent → Settings** in the editor.
- Add `ElevenLabsRealtimeController` to a GameObject that also has `MicrophoneCapture` and `StreamingAudioPlayer`. Enable **Connect On Start** to establish the socket automatically during `Start()`.
- The controller wraps `ElevenLabsRealtimeClient` (for WebSocket + `xi-api-key` auth) and reuses `Pcm16AudioStream` + `StreamingAudioPlayer` to decode PCM16 frames and enqueue them for playback.
- Set **Voice Override** on the controller (or in project settings) to send a `conversation_initiation_client_data` payload immediately after connect.
- Console logging is opt-in via the controller’s **Log Events** / **Log Audio Events** toggles so students can inspect `vad_score`, transcripts, tool calls, and ping/pong flow while iterating.
- Tool calls use the same `[RealtimeTool]` attributes as the OpenAI pipeline. When the agent emits `client_tool_call`, the controller looks up the registered method and replies with `client_tool_result` automatically.

### Notes from the JavaScript sample (`@elevenlabs/client`)

- The browser requests microphone access (`navigator.mediaDevices.getUserMedia`) *before* attempting to connect.
- `Conversation.startSession` accepts either a freshly signed URL (`signedUrl`) or a direct `agentId` for public agents and exposes callbacks for `onConnect`, `onDisconnect`, `onError`, and `onModeChange`.
- `onModeChange` reports an object that contains a `mode` string (`speaking`, `listening`, etc.), which you can mirror to drive UI feedback.
- Ending a call is simply `conversation.endSession()`, which closes the WebSocket and transitions the agent back to a listening state.
