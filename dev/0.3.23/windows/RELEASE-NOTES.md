0.3.23 development

- Meter grid: Edit and Play/Practice use the time-signature denominator for beat lines. 6/8 has six eighth-note divisions; 3/4, 7/8, 2/2 and smaller denominators use their own unit. The active beat frame and meter label also follow the written meter. Note timing and placement quantization are unchanged.
- Edit File: moved into the Edit toolbar. Removed Close. Clicking outside the dropdown, including the top header, dismisses it without activating the control behind it. Escape and the File toggle also close it.
- Edit Mixer: eight per-instrument volume faders below the timeline, with percentage values and a 100% marker. Range is 0 to 150%, matching v175. Uses the existing mix.instruments project data. HH includes open, closed and pedal sounds. Master Music/Drums settings remain separate. Mixer changes support grouped Undo/Redo, recovery and Save/reload. Gain headroom is prepared once to avoid reallocating samples during a fader change.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites passed, including mixed tuplets and variable-meter grids. 220 Windows runtime assertions passed, including actual audio voice gain ratios at 0/25/100/150 percent, continuous mixer editing without stopping playback or rebuilding clips, Undo/Redo, and saved-file readback. Browser interaction checks cover File dismissal, Mixer changes/undo, recovery and 6/8 Edit/Practice rendering.

No private projects or music are included. Native file-dialog interaction and subjective listening are not covered by automated tests. The user's Chrome download-completion issue and task 16 single-instance verification remain separately unresolved. Production root and WordPress content are unchanged.
