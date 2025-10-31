# Unity Voice Agent Plugin

Early-stage Unity package that wires OpenAI GPT Realtime (and later ElevenLabs) into a reusable voice-agent prefab. Mostly intentended for prototyping (I am doing this for a university lecture to help students get startet). The goal is to ship a simple, open source starting point installable through the Unity Package Manager via Git.

## Current Status
- ✅ Repository scaffolding and planning documents
- 🔄 Implementing initial OpenAI streaming voice loop (see `plan.md` for roadmap)

## Getting Started (Development)
1. Clone this repository and open the root Unity project (tested with `6000.2.9f1`).
2. The package lives under `Packages/com.dfin.voiceagent`. The project manifest references it via a local path for rapid iteration.
3. Install supporting dependencies via the Unity Package Manager:
   - `com.unity.nuget.newtonsoft-json` (official Json.NET fork, IL2CPP compatible).
   - `https://github.com/endel/NativeWebSocket.git#upm` (WebSocket layer that works on desktop, Android, iOS, Quest).
4. Open `Voice Agent → Settings` to create `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset` and enter development API keys.
5. Read `DEVELOPMENT.md` for coding standards and contribution workflow as they evolve.

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
