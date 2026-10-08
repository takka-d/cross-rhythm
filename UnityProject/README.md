# Cross Rhythm 0.3.17 development

Unity 6000.3.25f1. Windows and WebGL use the same Assets/Scripts source.
Run `./Build.ps1 -Target Both` from PowerShell with the licensed Unity Editor
and its Web Build Support installed. Builds go to `Builds/Windows` and `Builds/Web`.
Tests run before BuildPipeline.BuildPlayer. No license activation workaround,
DLL swap or player version-byte replacement is part of this build.

The production root remains 0.3.12. Verified development builds are published
under `/dev/0.3.17/`. The WordPress article remains a draft.

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
Practice pitch preservation remains a separate pending task.
