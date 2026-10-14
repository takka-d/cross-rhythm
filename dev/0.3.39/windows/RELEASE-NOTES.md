# Cross Rhythm 0.3.39 Windows development build

This development build advances five high-priority items that were still in progress or needed additional correction.

## Changes

- Restored controller pointer operations needed in Edit: the right stick can scroll, and pressing the right stick opens the right-click menu. Songs hover sound is latched so small cursor jitter does not repeatedly play it. Windows Title also has a cursor-accessible `Exit` button.
- Reworked the Windows startup display into one borderless full-monitor screen. Startup progress remains on that screen until the game is ready, Close and Esc share the same cancellation path, and a second launch brings the existing instance forward.
- Moved the Songs note-layout setting into the Preview row and renamed its choices to `Hand Notes on Top` / `Foot Notes on Top` (`手で叩くノーツが上` / `足で踏むノーツが上`). The same saved setting is shared with Practice and Config.
- Changed the Edit boundary buttons to the compact `|◀` and `▶|` labels without the unwanted horizontal line.
- Added exact whole-bar alignment for MIDI files whose leading offset represents complete source measures. Notes, tempo changes, meter changes and pedal releases move together. Small timing offsets remain untouched. Edit's Bar/Meter dialog can also jump directly to a typed bar before changing its meter.

## Verification completed before packaging

- Unity 6000.3.25f1 completed the final Windows and Web builds. Each build recorded 1,136 passing checks with no failed or fatal checks, including the core, stability and editor-arrangement suites.
- The packaged Windows runtime completed 653 checks with no failures. These cover exact-bar MIDI alignment, preservation of fine timing offsets, tempo and meter continuity, pedal release clipping, project save/reopen, typed-bar meter editing, controller input routing, Songs note layout and the Edit boundary controls.
- The Snowdome fixture aligned its complete one-bar audio lead so the imported 3/8 change begins at bar 51. The saved project reopened with the same aligned chart.
- The launcher integration check displayed its borderless full-monitor surface within 296 ms and passed status handoff, duplicate-instance, Esc cancellation and the shared Close cancellation path. An actual requested-project launch kept the same startup surface visible until the loaded Snowdome Edit screen was ready.
- Web save, folder-refresh and audio-drop bridge checks passed 50 assertions. MIDI and pedal parser follow-up checks passed another 31 assertions.
- The final Web build rendered Title, Songs, Edit and Bar/Meter in a browser with the 0.3.39 version visible and no console errors. The Songs note-layout row and Edit `|◀` / `▶|` controls were visually confirmed.

## Remaining device checks

- Physical controller movement, audible hover-effect balance, native file-dialog gestures and subjective playback timing require device-level confirmation.
- The Close button was visible on the actual startup surface. Its click coincided with the ready handoff, so termination specifically originating from that click was not separately observed; Esc termination and the shared cancellation handler passed the launcher test.

