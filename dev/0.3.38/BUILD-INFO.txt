# Cross Rhythm 0.3.38 Windows development build

This development build advances five high-priority items from the Cross Rhythm task list.

## Changes

- Added a small Windows startup window that appears before the Unity game window. It shows startup progress, supports Close and Esc, and keeps duplicate launches in one running game.
- Moved the Unity player under `Runtime` so the package root has one user-facing `CrossRhythm.exe`.
- Replaced the Edit start/end buttons with matching line-free `◀◀` and `▶▶` symbols.
- Changed Play's `Quit` action to `Stop`. Its dialog now offers Resume, Practice, or Songs.
- Practice now returns directly to Songs without a confirmation and can start a fresh scored Play session.
- Made triplet guide lines thinner and lighter while preserving beat and sixteenth-note guides.
- Cached visible Edit waveform columns so unchanged repaints do not repeat waveform range scans or allocate managed memory.

## Verification

- Unity 6000.3.25f1 Windows and Web builds completed with all core and stability tests.
- The Windows runtime completed 644 assertions, including the revised Play/Practice flow and Edit transport buttons.
- The launcher appeared within 282 ms in the integration check, forwarded runtime arguments, kept one child process on duplicate launch, and stopped the child through Esc.
- Independent MIDI and pedal checks passed 23 and 8 assertions.
- Web save, folder refresh and audio-drop bridge checks passed (50 checks total).
- Local browser inspection loaded 0.3.38 completely with no captured errors, warnings, or unhandled rejections. The final native runtime screenshots also confirmed the revised Edit and Practice layouts.
- The waveform benchmark used 640 visible columns across 2,000 repeated frames: direct recalculation took 175.3043 ms, cache reuse took 0.1698 ms, and steady cache use allocated 0 bytes.

## Verification limits

- The Windows Close button and Esc key call the same cancellation method. Esc was exercised end to end; direct UI automation of the Close button was not completed.
- The waveform benchmark covers Edit waveform range calculation. It does not by itself prove every GUI or long-duration freeze cause is removed.
- Browser click automation was not repeated after two failed click attempts; Play/Practice transitions were verified by the native runtime checks instead.
- Physical multi-controller use and native file-dialog gestures were not exercised in this build check.
