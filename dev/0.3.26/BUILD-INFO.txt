0.3.26 development

- Tambourine: HH input type and selected-note type, TB editor label and a ring-shaped performance glyph. Dedicated licensed recording, literal strengths and HH mixer gain work in Edit, Play and Practice. Pedal position does not change or choke this sound. Project save, recovery and Undo/Redo retain it. MIDI note 54 imports into HH/Tambourine.
- Export MIDI: Edit > File > Export MIDI writes a standard type-1 MIDI file with a conductor track and channel-10 GM drum track. Includes tempo changes, time signatures, five strengths, pedal onset and note durations, ending at the chart length. PPQ 32760 limits rounding to half a tick (less than 0.046 ms at 20 BPM). Muted notes are omitted. Buzz, open rimshot, tom rimshot and ride crash use approximate GM sounds; their count is reported after export. MIDI does not include the backing recording or all Cross Rhythm project metadata; keep the .crproj for editing.
- Export is independent of project Save. It does not mark the project saved, alter Undo or update Songs. Windows writes and verifies a separate .mid; Web uses a click-initiated save picker when available and verifies bytes after writing. Other browsers start a download and explicitly ask the user to check the downloaded file. Cancellation/errors restore controls and keep the draft.

Windows and WebGL share Unity 6000.3.25f1 source. Core regressions, native runtime checks, independent MIDI parsing with Mido 1.3.3, MIDI browser-bridge error handling and project-save bridge regressions passed. Browser UI evidence accompanies this development release.

Tambourine sample: Glen MacArthur, AVL Percussions, CC BY-SA 3.0; source/changes/license included in THIRD-PARTY-NOTICES and ThirdParty. https://github.com/studiorack/avl-percussions
MIDI format reference: https://midi.org/standard-midi-files

Native file-dialog interaction, physical controller/MIDI hardware and subjective listening are not covered by automated checks. MIDI hardware input/output is not implemented by this file-export change. The user's Chrome ZIP download-completion issue and task 16 single-instance verification remain separately unresolved. No private projects or recordings are included. Production root and WordPress content are unchanged.
