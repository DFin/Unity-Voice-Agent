# Unity Voice Agent Plugin

Minimalistic Unity package that wires OpenAI GPT Realtime and ElevenLabs Voice Agents into a reusable voice-agent prefab. Mostly intentended for prototyping (I am doing this for a university lecture to help students get started). The goal is to have a simple, open source starting point installable through the Unity Package Manager via Git.


##  Features

- Basic Realtime Voice AI integration for conversational AI via websockets
- Tool calls with an easy annotation method of C# code to register the tool with LLM 
- Event annotations that broadcast in-game happenings back to the model as structured user messages
- Sample prefabs (sphere + educational cube) that showcase audio playback, tooling, and event-driven guidance for students

##  Scope
**⚠️ WARNING ⚠️: This is just for prototyping and intended to get started. You shouldnt use this to ship anything. Your API key will be stored  as plain text in Assets/VoiceAgent/Resources/VoiceAgentSettings.asset** 

This is not intended to be a full library, but rather as a minimalistic starting point for websocket voice AI integration. I will not really maintain this beyound the scope required for my students. Feel free to make feature suggestions or report bugs - just no promise I will resolve the issue. 

If you are looking for a complete library for OpenAI check out this well maintained OpenAI library:
https://github.com/RageAgainstThePixel/com.openai.unity 

Also for Elevenlabs voice generation (but no agents) rather look at:
https://github.com/RageAgainstThePixel/com.rest.elevenlabs

If someones wants to maintain this feel free to fork it and I will link to your repo. 


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
4. Open `Voice Agent → Settings` to create `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset`, enter development API keys, and adjust options (model defaults to `gpt-realtime`, semantic VAD behavior, output sample rate). Configure the response **voice** per prefab via the `OpenAiRealtimeController` component.
5. Drop `OpenAiRealtimeController` on a GameObject (the required mic/audio components are added automatically). On play, the controller will create a fallback `AudioListener` if your scene does not already have one, then stream mic input and play back the model's audio responses in real time. If you need to stop playback, call `CancelActiveResponses()` manually.
   - The built-in streaming queue holds roughly 30 minutes of PCM audio by default; adjust `StreamingAudioPlayer.MaxBufferedSeconds` if you want a different memory/latency trade-off.
6. Try the sample prefabs under `Assets/VoiceAgent/Prefabs/`:
   - `SarcasticSphereAgent.prefab` wires in the realtime controller, an audio-reactive scaler, and a tool that lets the model move the sphere along the X-axis (clamped to `[-1, 1]`).
   - `EducationalCubeAgent.prefab` swaps the sphere for a cube with three clickable mini-cubes. Each click raises a `[RealtimeEvent]`, interrupts playback, and lets the agent guide students through the right sequence while exposing a reset tool.
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
- For more on the payload format and capabilities, see the official [OpenAI Function Calling guide](https://platform.openai.com/docs/guides/function-calling).

## Installing via UPM (Once Releases Start)
- Unity Package Manager → `Add package from git URL…`
- Use `https://github.com/dfin/unity-voice-agent.git#path=Packages/com.dfin.voiceagent`

## Documentation
- `plan.md` – phased technical roadmap (kept up to date during development).
- `DEVELOPMENT.md` – contributor setup, coding patterns (stub for now).
- Future student-facing tutorials and sample explanations will live under `Packages/com.dfin.voiceagent/Documentation~/`.
- Extra API notes live under `docs/`—keep them in sync with runtime behavior.

## License
MIT License (see `LICENSE`). Do as you please with this. If you want to use this for a game PLEASE, PLEASE, PLEASE DO! I want to see amazing AI characters in games and this has so much potential. Also if anyone needs help for Voice AI integration in games (eg in Unreal Engine etc) feel free to reach out. Happy to help. 

## Security Note
The editor configuration stores API keys in serialized assets for ease of use. Treat them as development-only credentials and rotate them if a project is shared.
