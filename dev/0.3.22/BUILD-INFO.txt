0.3.22 development

- Practice: the speed slider keeps its 0.25 to 2.00 range, with a visible 1.00 position marker and a single x1 reset button. Extra fine-step buttons and the idle pitch-preservation label are removed. Pitch-preserving playback remains enabled.
- Edit metadata: title and artist typing share one undo snapshot per field-edit session. Recovery archive writing waits until text focus leaves; text changes remain in the project immediately.
- Edit arrangement: drag Wave horizontally to shift the audio offset; the chart notes remain unchanged. A click still seeks. Shift/Ctrl gestures retain selection behavior. Wave moves support Undo/Redo, Escape cancellation and save/reload, including tempo changes.
- Edit placement: Grid preserves quantized operations; Free supports unsnapped placement, dragging, paste and duration edits. The mode is saved in the project. Existing files default to Grid.
- Save: after a verified write, refresh the matching existing Songs entry with the saved contents. Songs membership and selection stay unchanged; unrelated Edit files are not added. Later unsaved edits remain isolated. Saving also starts a new metadata undo group.
- Windows: Unity's built-in single-instance setting is enabled, but this item is NOT verified: the second hidden process did not exit in the isolated launch check. Task 16 remains open.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites and 209 Windows runtime assertions passed. A 6000-note / 40-character metadata test measured 1449.279 ms for the previous per-character snapshot operation and 34.5524 ms for grouped editing on this machine; these are local measurements, not guaranteed input latency. Native IME interaction and subjective audio quality remain manual checks. No private projects or music are included. The user's Chrome download-completion issue remains separately unresolved (0.3.19: ERR_BLOCKED_BY_CLIENT).

Additional 0.3.22 validation exercises the Windows Save-button handler, reads the actual output file, verifies title/artist and refreshes Songs without changing library membership or selection. It also checks isolation of later edits, unrelated saves, asynchronous saved snapshots, and navigation back to Edit. This establishes the missing Songs refresh; it does not claim to reproduce every possible native file-dialog or external-storage failure.
