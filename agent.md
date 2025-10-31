# Agent Handbook

Purpose: give human collaborators and coding agents a quick reference on how to work inside this repository without breaking workflows or letting docs drift.

## Core Expectations
- Always review `plan.md` before making changes; keep the roadmap updated when scope or sequencing evolves.
- Read the API reference notes in `docs/openai-gpt-realtime.md` and `docs/elevenlabs-voice-agent.md` prior to touching related systems. Update them whenever implementation deviates or new endpoints/settings are introduced.
- Maintain parity between code and documentation. New features must be reflected in:
  - `README.md` (student install/usage updates)
  - `Documentation~/` manuals for student-facing guides
  - `DEVELOPMENT.md` for contributor workflow changes
- Keep dependency notes current (NativeWebSocket, Newtonsoft JSON, others as added) so new contributors install the correct tooling.
- Configuration assets should remain in `Assets/VoiceAgent/Resources/VoiceAgentSettings.asset`; always update the editor tooling if the schema changes and remind users that credentials are stored in plain text.
- Prefer incremental commits with clear messages; never rewrite user-made history.

## Coding Guidelines
- Runtime code lives under `Packages/com.dfin.voiceagent/Runtime/`; editor tooling under `.../Editor/`; samples under `.../Samples~/`.
- Keep components modular and well-commented only where necessary for clarity—avoid noise comments.
- Use `apply_patch` (or Unity MCP structured edits when available) for small changes; ensure diffs stay focused.
- Run or document relevant tests/play-mode checks after significant changes; log verification steps in PR/commit messages.

## Communication
- Surface open questions in `plan.md` or commit messages so the roadmap stays authoritative.
- When unsure about scope, pause and ask the project owner rather than assuming.

Following these rules keeps the repo friendly for both students and future automation runs.
