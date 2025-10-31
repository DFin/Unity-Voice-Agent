# Development Notes (WIP)

This document will outline the contributor workflow as the project grows. Initial expectations:

- Keep runtime code under `Packages/com.dfin.voiceagent/Runtime/`, editor tooling under `.../Editor/`, and samples under `.../Samples~/`.
- Prefer small, composable components that students can inspect and override.
- Document any new subsystem in `Packages/com.dfin.voiceagent/Documentation~/` alongside code updates.
- Use the existing Unity project for rapid play mode testing; ensure the package remains installable in isolation.

Additional guidelines (style rules, testing strategy, PR template) will be added once core systems land.

