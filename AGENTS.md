# Dekteo Tool: context for the next developer

Read PROJECT_STATUS.md and docs/TEST_CHECKLIST.md before changing the application.
The older “Dekteo Tool — состояние проекта.md” is a historical report and contains outdated claims.

User intent:
- A Windows PNGTuber similar to veadotube mini: four PNG states, microphone-driven mouth, blinking, per-character effects.
- One full-window scene; floating controls, clear icons AND Russian labels; controls auto-hide and return on scene click or F8 without changing avatar geometry.
- Optional cursor-following PNG pupils: separately editable elliptical eye regions in character-local coordinates, smooth global cursor tracking.
- Keyboard/mouse/gamepad hand overlays are explicitly postponed. Keep them disabled/in development.
- Preserve user PNGs, profiles and existing calibration. The robot in screenshots is user content, not application branding.
- User wants explanations and a maintained handoff/checklist, not just code or repeated claims that features are done.

Implementation guardrails:
- Runtime scene lives in Views/AvatarView; do not reintroduce the old EyeTrackingService timer in settings panels.
- Animation changes RenderTransform values, not Canvas.Left/Top or mask geometry every frame.
- Layout and eye masks change only on calibration, image/canvas-size or character changes.
- Floating overlays and tool rails must share a parent for Panel.ZIndex to work.
- Keep a single microphone capture service at application scope; update WPF state on its dispatcher.
- Close the running app normally before replacing its executable. Save pending edits first.
- Update PROJECT_STATUS.md and docs/TEST_CHECKLIST.md after meaningful changes, distinguishing source checks, automated tests and actual user/device verification.
- Do not mark microphone voice reaction, OBS capture or visual smoothness verified merely because the build passes.

Build: dotnet build VeadotubeMy/VeadotubeMy --no-restore
Tests: dotnet run --project tests/Smoke --no-restore
Performance regression probe: dotnet run --project tests/Smoke --no-restore -- --render-probe
Optional real microphone probe: dotnet run --project tests/Smoke --no-restore -- --audio-probe

User preference (2026-09-18): conserve usage. Keep progress updates and handoff notes compact; inspect only relevant files, avoid repeated full reports and redundant tests. Still verify changed behavior.
