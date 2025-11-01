# Unity Voice Agent Plugin

Minimalistic Unity package that wires OpenAI GPT Realtime and ElevenLabs Voice Agents into a reusable voice-agent prefab. Mostly intentended for prototyping (I am doing this for a university lecture to help students get started). The goal is to have a simple, open source starting point installable through the Unity Package Manager via Git.

##  Scope

This is not intended to be a full library, but rather as a minimalistic starting point for websocket voice AI integration. 

If you are looking for a complete library for OpenAI check out this well maintained OpenAI library:
https://github.com/RageAgainstThePixel/com.openai.unity 

Also for Elevenlabs voice generation (not agents) rather look at:
https://github.com/RageAgainstThePixel/com.rest.elevenlabs


## Current Status
- ✅ Repository scaffolding and planning documents
- ✅ Realtime OpenAI voice loop with large streaming buffer & server-driven interruption handling
- ✅ Attribute-based function calling: annotate any `MonoBehaviour` method with `[RealtimeTool]` to expose it as an OpenAI tool
- ✅ Sample prefab (`SarcasticSphereAgent`) demonstrating realtime playback, audio-reactive scaling, and tool-controlled movement
- 🔄 Expanding debugging tooling and ElevenLabs support (see `plan.md` for roadmap)

## Getting Started (Development)
1. Clone this repository and open the root Unity project (tested with `6000.2.9f1`).
2. The package lives under `Packages/com.dfin.voiceagent`. The project manifest references it via a local path for rapid iteration.
3. Install supporting dependencies via the Unity Package Manager:
   - `com.unity.nuget.newtonsoft-json` (official Json.NET fork, IL2CPP compatible).
   - `https://github.com/endel/NativeWebSocket.git#upm` (WebSocket layer that works on desktop, Android, iOS, Quest).
4. Open `Voice Agent → Settings` to create `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset`, enter development API keys, and adjust options (model defaults to `gpt-realtime`, semantic VAD behavior, voice, output sample rate).
5. Drop `OpenAiRealtimeController` on a GameObject (the required mic/audio components are added automatically). On play, the controller will create a fallback `AudioListener` if your scene does not already have one, then stream mic input and play back the model's audio responses in real time. If you need to stop playback, call `CancelActiveResponses()` manually.
   - The built-in streaming queue holds roughly 30 minutes of PCM audio by default; adjust `StreamingAudioPlayer.MaxBufferedSeconds` if you want a different memory/latency trade-off.
6. Try the sample prefab under `Assets/VoiceAgent/Prefabs/SarcasticSphereAgent.prefab`. It wires in the realtime controller, an audio-reactive scaler, and a tool that lets the model move the sphere along the X-axis (clamped to `[-1, 1]`) using the new annotation system.
7. Read `DEVELOPMENT.md` for coding standards and contribution workflow as they evolve.

## Function Calling via Annotations

Expose Unity methods to the OpenAI Realtime model with one attribute:

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

- `[RealtimeTool]` marks the method; the optional `name` argument overrides the function name exposed to the model.
- `[RealtimeToolParam]` documents each argument and controls whether it is required (defaults to `true`).
- Supported parameter types: strings, booleans, numeric types, and enums. Optional parameters must be nullable or supply a default value.
- `OpenAiRealtimeController` discovers tools automatically at runtime, advertises them in `session.update`, and invokes them when the model issues a `function_call`. Return values are serialized and streamed back; void methods send a default “Tool call handled.” message.
- Check `SphereMovementTool` for a concrete example included in the package.

## Installing via UPM (Once Releases Start)
- Unity Package Manager → `Add package from git URL…`
- Use `https://github.com/<your-org-or-username>/unity-voice-agent.git#path=Packages/com.dfin.voiceagent`

## Documentation
- `plan.md` – phased technical roadmap (kept up to date during development).
- `DEVELOPMENT.md` – contributor setup, coding patterns (stub for now).
- Future student-facing tutorials and sample explanations will live under `Packages/com.dfin.voiceagent/Documentation~/`.
- Extra API notes live under `docs/`—keep them in sync with runtime behavior.

## License
MIT License (see `LICENSE` once committed).

## Security Note
The editor configuration stores API keys in serialized assets for ease of use. Treat them as development-only credentials and rotate them if a project is shared.
