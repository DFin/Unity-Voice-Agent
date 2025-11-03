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
- Each `OpenAiRealtimeController` exposes a **Voice Override** field so you can choose a voice per instance. Leave it empty to fall back to the value stored in `VoiceAgentSettings`.
- Turn detection defaults to **Semantic VAD** with eagerness `low`. The settings asset lets you switch between `None`, `Server VAD`, or `Semantic VAD` and tune the corresponding parameters.

**Runtime Scaffolding (Phase 1)**

- `NativeWebSocketTransport` wraps the NativeWebSocket package so we can connect on desktop, Android, iOS, Quest, and WebGL.
- `OpenAiRealtimeController` MonoBehaviour loads settings, connects, sends a `session.update`, dispatches NativeWebSocket messages, forwards mic audio as `input_audio_buffer.append`, and handles response audio playback (it also spawns a fallback `AudioListener` when a scene lacks one). It tracks in-flight `response_id`s so it can cancel/clear audio buffers when the user starts speaking again.
- `MicrophoneCapture` publishes raw float sample buffers (16 kHz by default) to feed into the realtime API.
- `OpenAiAudioStream` forwards every `response.output_audio.delta` / `response.audio.delta` payload as PCM16 samples, and `StreamingAudioPlayer` pushes them into a streaming AudioClip so playback begins immediately. Basic linear resampling covers 24 kHz → Unity output rates, and the queue currently buffers ~30 minutes of audio before trimming. You can call `OpenAiRealtimeController.CancelActiveResponses()` to forward `response.cancel` and buffer-clear messages when needed.
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
        "turn_detection": { "type": "semantic_vad", "eagerness": "low" },
        "audio": {
          "output": { "voice": "alloy" }
        },
        "instructions": "You are a concise assistant."
      }
    }

Notes:
- Pick `semantic_vad` for the intent-aware detector, `server_vad` for classic amplitude VAD, or `null` to disable automatic turn handling entirely.
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

### Unity integration

1. **Add the annotations**
   ```csharp
   using DFIN.VoiceAgent.OpenAI;

   public class MoveCube : MonoBehaviour
   {
       [RealtimeTool("Moves the cube to an absolute X coordinate between -1 and 1.")]
       public void SetCubeX(
           [RealtimeToolParam("Absolute world X position (-1 .. 1).")] float x)
       {
           transform.position = new Vector3(Mathf.Clamp(x, -1f, 1f), transform.position.y, transform.position.z);
       }
   }
   ```
   - `[RealtimeTool]` applies to the method. The optional `name:` argument lets you override the function name exposed to the model; otherwise the method name is used.
   - `[RealtimeToolParam]` describes each argument. `required` defaults to `true`.

2. **Supported parameter types**

   | C# type                       | JSON schema type | Notes                                         |
   | ----------------------------- | ---------------- | --------------------------------------------- |
   | `string`                      | `"string"`       |                                               |
   | `bool`                        | `"boolean"`      |                                               |
   | Any integer type (`int`, …)   | `"integer"`      |                                               |
   | `float`, `double`, `decimal`  | `"number"`       |                                               |
   | `enum`                        | `"string"`       | Enum values are emitted as an `"enum"` array. |

   Optional parameters must either be nullable or have a default value. Unsupported types will log a warning and the method is skipped.

3. **Discovery & session update**

   `OpenAiRealtimeController` scans every `MonoBehaviour` in the scene right before it sends `session.update`. Each annotated method becomes an entry in the `tools` array and `tool_choice` is set to `"auto"`.

4. **Invocation**

   When the model issues a `function_call`, the controller:
   - Parses the arguments into a `JObject` and converts them to the method’s parameter types.
   - Invokes the method on the Unity main thread.
   - Serializes the return value (if any) and streams it back via `conversation.item.create` followed by `response.create`. `void` methods send a default “Tool call handled.” message.
   - If the method throws, the error message is forwarded to the model so it can recover.

5. **Scene workflow**

   Drop the component with the annotated method into the scene (or include it on a prefab). No additional registration is required—enter Play mode and the controller will advertise the tool automatically.

---

## 7) Event annotations (user messages)

`[RealtimeEvent]` mirrors the tool annotation flow but is optimized for sending structured user messages back to the model whenever something happens in the scene. Each annotated method becomes an event definition the controller can invoke at runtime.

```csharp
[RealtimeEvent("Event: user pressed red cube", name: "red_cube_pressed")]
private void RedCubePressed()
{
    // Optional: run local gameplay logic before the message is sent.
}
```

Call `OpenAiRealtimeController.PublishEvent("red_cube_pressed")` to raise the event. By default the controller:

- Interrupts any in-flight audio by calling `CancelActiveResponses()`.
- Creates a `conversation.item.create` payload with role `user` and the attribute’s message (prefixed with `Event:` in our samples so prompts can distinguish them).
- Sends a follow-up `response.create` so the assistant can react immediately.

If you need to push arbitrary text without an attribute, call `SendUserMessage("Event: ...")` directly—both helpers share the same plumbing so they remain in sync with future API changes.

See `EducationalCubeAgent` and `EducationalCubeButton` for a minimal prefab that lights up coloured cubes, raises `[RealtimeEvent]` messages, and exposes a tool that resets the puzzle.

**Tip:** The `OpenAiRealtimeController` inspector exposes **Request Initial Response On Connect**. When enabled (the sample prefabs keep it on), the controller calls `RequestAssistantResponseAsync`, which issues a `response.create` immediately after `session.update` so the assistant can greet the user without waiting for input.

---

## 8) Common server events to expect (non‑exhaustive)

- `session.created` / `session.updated`
- `input_audio_buffer.speech_started` / `speech_stopped` (when using server VAD)
- `response.done` (status `cancelled`, reason `turn_detected`) when the server interrupts output
- `response.output_audio.delta` / `response.audio.delta` and their corresponding `…done`
- Tool‑call events on `response.*` (names may evolve; switch on the `type` string and read payload)

---

## 9) Troubleshooting

- 4xx during connect → verify **Authorization** header and model id.
- No audio received → ensure `modalities` include `"audio"` and a **voice** is set before first audio output.
- VAD too eager/slow → tweak `turn_detection` thresholds in `session.update`.
- User speech overlaps assistant audio → the controller now issues `response.cancel`, `output_audio_buffer.clear`, and `input_audio_buffer.clear` when mic RMS spikes; tune the threshold or call `CancelActiveResponses()` manually if needed.
