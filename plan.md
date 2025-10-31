# Unity Voice Agent – Implementation Plan

## Guiding Principles
- Deliver an open-source, student-friendly Unity package installable via UPM (Git URL).
- Optimize for a fast path to a working voice conversation with OpenAI GPT Realtime; visuals are deferred until that loop is solid.
- Keep the architecture modular so WebSocket/session utilities and voice pipelines are reusable (e.g., for ElevenLabs, future function-calling workflows).
- Favor minimal dependencies and built-in Unity systems unless a clear benefit outweighs onboarding cost.

## Phase 0 – Repository & Package Scaffolding
- [x] Convert project folder into a Unity package layout (`Packages/com.dfin.voiceagent`) while keeping sample project assets for rapid testing.
- [x] Establish assembly definitions for runtime, editor, and sample code to keep dependencies clean.
- [x] Set up baseline documentation skeleton (`README.md`, `DEVELOPMENT.md`, `plan.md`) and licensing.
- [x] Keep API-specific references under `docs/` (e.g., OpenAI GPT Realtime, ElevenLabs) synced with implementation changes and contributor notes (`agent.md`).
- [x] Configure package metadata (display name, description, versioning strategy, UPM-compatible structure).

## Phase 1 – Core Realtime Voice Loop (OpenAI First Priority)
- [x] Implement configuration assets & editor window for API keys and session params (no scene inspector reliance).
- [ ] Create WebSocket client wrapper tailored to OpenAI GPT Realtime streaming (handles authentication, reconnects, event callbacks).
- [ ] Add microphone capture component (Unity `Microphone` API fallback, with extensibility for other input systems).
- [ ] Implement audio playback pipeline (PCM decoding, `AudioSource` streaming) with optional spatialization flag for VR targets.
- [ ] Build minimal runtime controller prefab that wires config -> mic capture -> OpenAI streaming -> audio playback; unit/manual tests via console logging only (no visuals yet).
- [ ] Provide lightweight debugging hooks (logging categories, connection status) for students troubleshooting auth/network issues.
- [x] Document dependency setup (NativeWebSocket, Newtonsoft JSON) and keep installation instructions aligned with package requirements.

## Phase 2 – ElevenLabs Voice Output Integration
- [ ] Reuse WebSocket/shared transport layer where possible; isolate ElevenLabs-specific session handling.
- [ ] Implement separate prefab for ElevenLabs voice pipeline, sharing base classes for microphone handling and playback when feasible.
- [ ] Support switching/combining OpenAI text generation with ElevenLabs TTS output pathways.
- [ ] Ensure spatial audio and volume-based feedback outputs remain configurable.

## Phase 3 – Runtime Feedback Components
- [ ] Extend prefabs with optional feedback components (e.g., material emissive/brightness scaling based on output volume).
- [ ] Provide simple scripting hooks/interfaces so students can respond to agent states without modifying core scripts.
- [ ] Document minimal examples (code snippets) for hooking custom visuals once the audio loop is verified.

## Phase 4 – Sample Scenes & Function Calling
- [ ] Create simple sample scene: sphere agent responds to voice by changing color/emissive intensity/scale while speaking.
- [ ] Add sample demonstrating function calling (“move cube 1 m left”) with clean API for registering callable actions.
- [ ] Include quick-start instructions guiding students from package import to sample scene playtest.

## Phase 5 – Packaging, QA & Documentation
- [ ] Finalize README (installation, quick start), DEVELOPMENT (contributing, coding standards), and in-editor tooltips.
- [ ] Record known limitations, latency considerations, and troubleshooting tips.
- [ ] Set up automated lint/build (if viable) or document manual test steps; prep versioned release tags.
- [ ] Validate package installation via Git URL from a clean Unity project.

## Open Questions / Follow-ups
- Confirm preferred logging verbosity and whether to include optional in-editor connection monitor.
- Decide if we support Windows/macOS/Linux equally in MVP or note platform caveats.
- Clarify long-term plans for local model compatibility or keep scope cloud-only.
- Determine contribution workflow (PR guidelines, issue templates) for the open-source repo when ready.
