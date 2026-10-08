0.3.21 development

- Practice: the speed slider keeps its 0.25 to 2.00 range, with a visible 1.00 position marker and a single x1 reset button. Extra fine-step buttons and the idle pitch-preservation label are removed. Pitch-preserving playback remains enabled.
- Edit metadata: title and artist typing share one undo snapshot per field-edit session. Recovery archive writing waits until text focus leaves; text changes remain in the project immediately.
- Edit arrangement: drag Wave horizontally to shift the audio offset; the chart notes remain unchanged. A click still seeks. Shift/Ctrl gestures retain selection behavior. Wave moves support Undo/Redo, Escape cancellation and save/reload, including tempo changes.
- Edit placement: Grid preserves quantized operations; Free supports unsnapped placement, dragging, paste and duration edits. The mode is saved in the project. Existing files default to Grid.
- Windows: Unity's built-in single-instance setting is enabled, but this item is NOT verified: the second hidden process did not exit in the isolated launch check. Task 16 remains open.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regression suites and 201 Windows runtime assertions passed. A 6000-note / 40-character metadata test measured 1615.903 ms for the previous per-character snapshot operation and 33.1611 ms for grouped editing on this machine; these are local measurements, not guaranteed input latency. Native IME interaction and subjective audio quality remain manual checks. No private projects or music are included. The user's Chrome download-completion issue remains separately unresolved (0.3.19: ERR_BLOCKED_BY_CLIENT).
