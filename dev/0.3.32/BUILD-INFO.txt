Cross Rhythm 0.3.32 development

- Bar / Meter can keep absolute note, pedal and tempo timing while changing occupied bar boundaries. Overflow notes are reassigned, with end bars retained. Undo, Redo and saved projects preserve the result. Existing shift-following-bars mode remains available.
- Default editor Type labels now use instrument names (Hi-hat, Snare, High/Mid/Floor tom, Kick); Ride crash is explicit. Lane menu, note inspector and context menu share the same labels.
- Rebalanced quiet hi-hat, ride and ride-bell sample gains without changing MIDI strengths, per-project mixer values, strength ratios or Ride Crash gain.
- Private MIDI fixture verification: PPQ 480, meter change at tick 73440 = beat 153 = bar 52. One-bar disagreement with intended music position remains under investigation; import does not silently shift source timing.

Development release only. Production and the WordPress draft were not changed. Physical listening and native file-dialog gestures are not verified. Browser ZIP download completion remains unresolved; the ZIP is also provided as a verified local copy.
