# Cross Rhythm 0.3.19 development

Unity 6000.3.25f1. Windows and WebGL use the same Assets/Scripts source.
Run `./Build.ps1 -Target Both` from PowerShell with the licensed Unity Editor
and its Web Build Support installed. Builds go to `Builds/Windows` and `Builds/Web`.
Tests run before BuildPipeline.BuildPlayer. No license activation workaround,
DLL swap or player version-byte replacement is part of this build.

The production root remains 0.3.12. Verified development builds are published
under `/dev/0.3.19/`. The WordPress article remains a draft.

Changes: common folder row in Songs/Config; Songs > Edit opens an independent
editable copy; File in Edit header; Back in Play/Practice; Practice speed
buttons (0.25–2); ADTOF local companion under Tools/ADTOF.

ADTOF: run Start ADTOF.cmd, then Edit > Analyze > Check > ADTOF.
Review the generated count and Apply; Undo reverses Apply. The model is
downloaded separately. See Tools/ADTOF/README.txt for setup and model license.
No personal song projects/audio are bundled.

LegacyWeb is the earlier HTML reference, not the current Unity implementation.
Its old ADTOF title did not correspond to an implemented analysis client.

0.3.14: six-cue count-in (1, 2, 1, 2, 3, 4); optional Songs preview
(default OFF, remembered); background/cancellable audio decoding; Shift+left
range selection and Ctrl+left multi-selection; unsaved-change confirmation
for navigation and closing. Leave discards the current editor draft without modifying saved files. Browser close
uses the browser-native unsaved warning; save before closing to update a file.

0.3.15: Edit navigation also works while File/context menus are open.
New Project uses the unsaved confirmation. Cancel keeps edits; Leave clears
the editor and its recovery draft; Save & Continue requires a verified save.
Count-in keeps four visual beats in 4/4, with six audible cues over two bars.
Other meters use two bars of the initial meter, counting each denominator beat.

0.3.16: Tuplet grid-line clicks tolerate float pointer rounding.
Dragging preserves the onset phase and relative timing. MIDI display cells
respect PPQ precision; onset times stay unchanged. The Edit grid also shows
actual imported onsets. Covers 3/5/7/9/11/13 divisions, mixed grids and 7/8.

0.3.17: Tempo changes are editable under Edit > Tempo, including inside a bar.
Bar and Beat fields are one-based; Beat is measured in quarter notes.
Set adds/replaces a point; Remove restores the preceding tempo. Initial tempo
is edited with Set and cannot be removed. Use Cursor copies the playhead position.
MIDI Set Tempo events are imported without quantizing note timing.

Chart timing uses optional tempoChanges: [{measure: zero-based bar index,
beat: zero-based local quarter beat, bpm: 20..600}]. The existing bpm is the
initial tempo; charts without tempoChanges retain constant-tempo behavior.
All playback, hit judgment, waveform, duration/difficulty and audio analysis
use the same integrated beat/second map. Save/reload and Undo/Redo retain it.
Practice pitch preservation is implemented for Windows and Web in 0.3.19 below.

0.3.18: Drop one .mid/.midi file (up to 32 MB) onto Edit in Windows or Web.
The existing Import MIDI command uses the same overlap review.
Same instrument and exact MIDI tick positions are reviewed per instrument:
prefer a Type, choose stronger/softer hits, or keep all. Different ticks and
different instruments are preserved, without quantization. Hi-hat pedal
ranges are derived after the choices, so open/closed state stays consistent.
Cancel preserves the chart; Import is one Undo step. Additional drops and
background edits are blocked while reviewing or loading.

0.3.19 RELEASE SCOPE: Windows and Web development builds. The Windows package
is unchanged; download it directly from GitHub Releases. Web now uses one
AudioContext clock for the chart, judgment and streamed backing at every speed,
including unchanged PCM at 1x. Unity drum scheduling is converted at the boundary.

Web verification includes 0.25/0.55/0.60/1/2x, seek, pause, mode switching,
preparation cancellation, count-in, Play and a main-thread stall. The final
UI run stopped on an obsolete test expecting a full-song AudioBuffer node;
the preserved recording was then checked against the actual streamed-buffer
contract without another audio run. See dev/0.3.19/web-verification.json.
Physical audio-device latency remains outside these source/clock checks.

0.3.19: Practice preserves musical pitch across speeds 0.25 to 2.00.
Windows and Web use the same Signalsmith Stretch processor. Only a bounded
six-second playback buffer is prepared, instead of expanding the entire song.
The chart waits while preparing initial audio; processor latency is removed.
Seeking and speed changes restart from the same source position. Switching
Normal/Pro at the same speed keeps the current audio running.
Extreme speed changes may alter timbre/transients. Audio device latency is
separate from the synchronized source/beat clock.

Audio processor dependencies: Signalsmith Stretch 1.3.2 and Signalsmith Linear
0.6.4, both MIT; licenses and pinned revisions are in ThirdParty and
THIRD-PARTY-NOTICES.txt. Rebuild the Windows x64 DLL with
Tools/AudioStretch/Build-Windows.ps1 using Visual Studio C++ build tools.
WebGL compiles the same bridge and headers directly with Unity.

The application icon is an original crossed-drumsticks design, used by the
Windows player and the Web favicon. Vector source: Branding/cross-rhythm-icon.svg.
Config preset names now describe A-button positions (right or bottom); mappings
and saved controller preferences are unchanged. Connected device names remain
as supplied by the operating system/browser to identify actual hardware.


0.3.24 development

- Songs: Start, Practice and Edit accept input while preview audio is loading. Start/Practice enter their screen immediately and start the count-in only after the selected audio is ready. Back cancels the pending start. Open Folder stays available while audio loads.
- Practice: a 0.25 to 2.00 speed slider is restored alongside the existing fine-step/reset controls. The displayed value changes during a drag; audio speed is applied on release to avoid repeatedly restarting preparation. Pitch preservation remains enabled.
- Config: controller reset presets explicitly say "A button on right" or "A / × button on bottom", with Japanese equivalents. Both say they restore the default keys/buttons.
- Count-in: for even meter numerators, two equally spaced cues in the first bar precede one full bar of denominator beats. 4/4: 1,2 / 1,2,3,4. 6/8: 1,2 / 1,2,3,4,5,6. The displayed meter and beat grid remain the chart's meter. Odd meters still count each denominator beat for two bars; beat grouping is not inferred from the numerator.

Meter rationale: asymmetrical 5/8 can group 2+3 or 3+2, and 7/8 can group 2+2+3 or 3+2+2. A meter alone does not identify the song's grouping. Source: https://pressbooks.uiowa.edu/twentieth-and-twenty-first-century-music/chapter/meter/ . This fallback is an application choice, not a claimed universal count-in convention.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regressions and 189 Windows runtime assertions passed. Browser device/output latency is not measured. No private projects or music are included. Browser download completion on the user's Chrome remains a separate unresolved issue (0.3.19: ERR_BLOCKED_BY_CLIENT).


0.3.21 development

- Practice: the speed slider keeps its 0.25 to 2.00 range, with a visible 1.00 position marker and a single x1 reset button. Extra fine-step buttons and the idle pitch-preservation label are removed. Pitch-preserving playback remains enabled.
- Edit metadata: title and artist typing share one undo snapshot per field-edit session. Recovery archive writing waits until text focus leaves; text changes remain in the project immediately.
- Edit arrangement: drag Wave horizontally to shift the audio offset; the chart notes remain unchanged. A click still seeks. Shift/Ctrl gestures retain selection behavior. Wave moves support Undo/Redo, Escape cancellation and save/reload, including tempo changes.
- Edit placement: Grid preserves quantized operations; Free supports unsnapped placement, dragging, paste and duration edits. The mode is saved in the project. Existing files default to Grid.
- Windows: Unity's built-in single-instance setting is enabled, but this item is NOT verified: the second hidden process did not exit in the isolated launch check. Task 16 remains open.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites and 201 Windows runtime assertions passed. A 6000-note / 40-character metadata test measured 1615.903 ms for the previous per-character snapshot operation and 33.1611 ms for grouped editing on this machine; these are local measurements, not guaranteed input latency. Native IME interaction and subjective audio quality remain manual checks. No private projects or music are included. The user's Chrome download-completion issue remains separately unresolved (0.3.19: ERR_BLOCKED_BY_CLIENT).


0.3.22 development

- Practice: the speed slider keeps its 0.25 to 2.00 range, with a visible 1.00 position marker and a single x1 reset button. Extra fine-step buttons and the idle pitch-preservation label are removed. Pitch-preserving playback remains enabled.
- Edit metadata: title and artist typing share one undo snapshot per field-edit session. Recovery archive writing waits until text focus leaves; text changes remain in the project immediately.
- Edit arrangement: drag Wave horizontally to shift the audio offset; the chart notes remain unchanged. A click still seeks. Shift/Ctrl gestures retain selection behavior. Wave moves support Undo/Redo, Escape cancellation and save/reload, including tempo changes.
- Edit placement: Grid preserves quantized operations; Free supports unsnapped placement, dragging, paste and duration edits. The mode is saved in the project. Existing files default to Grid.
- Save: after a verified write, refresh the matching existing Songs entry with the saved contents. Songs membership and selection stay unchanged; unrelated Edit files are not added. Later unsaved edits remain isolated. Saving also starts a new metadata undo group.
- Windows: Unity's built-in single-instance setting is enabled, but this item is NOT verified: the second hidden process did not exit in the isolated launch check. Task 16 remains open.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites and 209 Windows runtime assertions passed. A 6000-note / 40-character metadata test measured 1449.279 ms for the previous per-character snapshot operation and 34.5524 ms for grouped editing on this machine; these are local measurements, not guaranteed input latency. Native IME interaction and subjective audio quality remain manual checks. No private projects or music are included. The user's Chrome download-completion issue remains separately unresolved (0.3.19: ERR_BLOCKED_BY_CLIENT).

Additional 0.3.22 validation exercises the Windows Save-button handler, reads the actual output file, verifies title/artist and refreshes Songs without changing library membership or selection. It also checks isolation of later edits, unrelated saves, asynchronous saved snapshots, and navigation back to Edit. This establishes the missing Songs refresh; it does not claim to reproduce every possible native file-dialog or external-storage failure.


0.3.23 development

- Meter grid: Edit and Play/Practice use the time-signature denominator for beat lines. 6/8 has six eighth-note divisions; 3/4, 7/8, 2/2 and smaller denominators use their own unit. The active beat frame and meter label also follow the written meter. Note timing and placement quantization are unchanged.
- Edit File: moved into the Edit toolbar. Removed Close. Clicking outside the dropdown, including the top header, dismisses it without activating the control behind it. Escape and the File toggle also close it.
- Edit Mixer: eight per-instrument volume faders below the timeline, with percentage values and a 100% marker. Range is 0 to 150%, matching v175. Uses the existing mix.instruments project data. HH includes open, closed and pedal sounds. Master Music/Drums settings remain separate. Mixer changes support grouped Undo/Redo, recovery and Save/reload. Gain headroom is prepared once to avoid reallocating samples during a fader change.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites passed, including mixed tuplets and variable-meter grids. 220 Windows runtime assertions passed, including actual audio voice gain ratios at 0/25/100/150 percent, continuous mixer editing without stopping playback or rebuilding clips, Undo/Redo, and saved-file readback. Browser interaction checks cover File dismissal, Mixer changes/undo, recovery and 6/8 Edit/Practice rendering.

No private projects or music are included. Native file-dialog interaction and subjective listening are not covered by automated tests. The user's Chrome download-completion issue and task 16 single-instance verification remain separately unresolved. Production root and WordPress content are unchanged.


0.3.24 development

- MIDI overlap review: select exactly one source event at each shared instrument/tick. Shows GM source sound, MIDI pitch, mapped Type, track/channel, raw velocity and strength, bar/beat/time and exact tick. Per-instrument bulk selection applies only where the selected source uniquely exists; missing or ambiguous positions still require an explicit choice. There is no stronger/softer or keep-all policy. Import stays disabled until every collision is resolved. Other hits, exact tuplets, tempo changes, cancel, single-step Undo/Redo and hi-hat state are preserved.
- Edit Input Type: choose the type to place from each instrument's label column. This changes future placement only. Instrument-name clicks still select the lane and Ctrl adds to selection. Choices are project-specific, undoable, saved and recovered. Normal note selection and context-menu editing remain separate.
- Controller: generic XInput backend names are no longer claimed as the device model. Shows an available device-reported product or Model unavailable. Added a live pressed-button indicator and a separate label/confirm-position choice (A on right or A/cross on bottom) that keeps custom performance mappings. Reset controls remain explicit. Physical recognition of the user's controller has not been verified.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites passed, including exact MIDI selection, mixed tuplets, variable-meter grids, editor gestures, per-lane types and controller identity handling. Windows runtime assertions cover the actual import/undo/save pipeline, per-lane pointer placement, saved-file readback and synthetic InputSystem button events. Browser interaction evidence is stored with this release.

No private projects or music are included. Native file-dialog interaction, the user's physical controller and subjective listening are not covered by automated tests. The user's Chrome download-completion issue and task 16 single-instance verification remain separately unresolved. Production root and WordPress content are unchanged.
