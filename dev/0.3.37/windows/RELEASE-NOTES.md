# Cross Rhythm 0.3.37 development

- Added controller navigation across the game UI, including left-stick pointer control, directional focus movement, confirm/back handling, scrolling and editor operations. Generic controllers are no longer presented as Xbox controllers, and reserved navigation inputs are removed from older saved performance mappings.
- Added a responsive startup screen for Windows while the saved project folder and editor document are restored. The startup screen shows progress and provides Close/Escape handling.
- Reduced long-chart playback work by using forward judgement cursors, cached score/time-signature data, binary-search lookups and reusable buffers.
- Undo and Redo now display the blocking progress state before history restoration starts.
- Moved the lane-order control beside Preview and clarified its labels as Top: Hands / Top: Feet (上段: 手 / 上段: 足).

## Verification status

- Windows and Web builds completed with the core and stability checks passing.
- Web save, folder refresh and audio-drop bridge checks passed (50 checks total).
- Local browser inspection reached 0.3.37 without console errors, confirmed Japanese and English lane-order labels, the Edit layout, and Arrow/Enter navigation. The one-note Undo completed too quickly to capture its transient progress frame.
- The Windows runtime run completed 253 task assertions before stopping at the startup-paint assertion in the hidden test window. A complete runtime pass was not recorded.
- MIDI and pedal parser follow-up checks were not run because their runtime-generated input files were not produced before that stop.
- Physical multi-controller operation, an hours-long real-time session and native operating-system file-dialog gestures remain unverified.

Development release only. Production and WordPress were not changed.
