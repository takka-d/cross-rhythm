0.3.31 development

- Config adds Hands Above / Feet Above. Play and Practice swap complete hand/foot lane groups, including guides, key displays, pedal holds and hit effects. Keyboard/controller mappings and project data stay unchanged; the preference persists per platform.
- Indexed note and pedal queries remove repeated full-chart scans from stage positions, editor drawing, grid generation and judgement lookup. Long holds and buzz notes remain visible across viewport boundaries. This addresses measured rendering work; remaining freeze reports need further confirmation.
- Grid choices use musical note values: eighth-note and sixteenth-note triplets, quintuplets and septuplets. Changing the input grid retains existing note onsets, drawn widths, explicit and legacy implicit durations, and pedal releases. Undo/Redo and save/reopen preserve them.
- Insert Bar inherits the selected bar's meter, including 6/8 and 7/8. Bar / Meter opens an explicit dialog from the ruler or Song panel, with selected-bar navigation, meter fields, Apply Meter, insertion, protected empty-bar removal and extension to audio length.
- Edit mute controls sit beside the waveform and instrument lanes. HH and pedal share their existing hi-hat monitor mute. Drums mute is beside the waveform; Metronome is beside Grid. Mixer keeps volume faders.
- Shared controls distinguish menus with a down arrow, dialogs with an ellipsis, on/off switches, radio choices and underlined tabs. Existing hover, focus and sound feedback remain. Native and Web file buttons use matching dialog cues.
- Ride returns to a gold cymbal palette consistent with the other notes. Its approved silhouette, tilt, size and six strength heights are unchanged.
- Title copy is English in both language modes: Play the music you own to the fullest / Feel the groove through drums.

Windows and Web share Unity 6000.3.25f1 source. Verification uses synthetic fixtures; OS-native file picker gestures and Windows rendered layout remain unverified. The earlier browser ZIP download-completion issue remains unresolved. No private projects or recordings are included. Production root and WordPress content are unchanged.
