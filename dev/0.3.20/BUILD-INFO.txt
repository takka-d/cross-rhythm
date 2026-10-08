0.3.20 development

- Songs: Start, Practice and Edit accept input while preview audio is loading. Start/Practice enter their screen immediately and start the count-in only after the selected audio is ready. Back cancels the pending start. Open Folder stays available while audio loads.
- Practice: a 0.25 to 2.00 speed slider is restored alongside the existing fine-step/reset controls. The displayed value changes during a drag; audio speed is applied on release to avoid repeatedly restarting preparation. Pitch preservation remains enabled.
- Config: controller reset presets explicitly say "A button on right" or "A / × button on bottom", with Japanese equivalents. Both say they restore the default keys/buttons.
- Count-in: for even meter numerators, two equally spaced cues in the first bar precede one full bar of denominator beats. 4/4: 1,2 / 1,2,3,4. 6/8: 1,2 / 1,2,3,4,5,6. The displayed meter and beat grid remain the chart's meter. Odd meters still count each denominator beat for two bars; beat grouping is not inferred from the numerator.

Meter rationale: asymmetrical 5/8 can group 2+3 or 3+2, and 7/8 can group 2+2+3 or 3+2+2. A meter alone does not identify the song's grouping. Source: https://pressbooks.uiowa.edu/twentieth-and-twenty-first-century-music/chapter/meter/ . This fallback is an application choice, not a claimed universal count-in convention.

Windows and WebGL built from the same Unity 6000.3.25f1 source. Core regressions and 189 Windows runtime assertions passed. Browser device/output latency is not measured. No private projects or music are included. Browser download completion on the user's Chrome remains a separate unresolved issue (0.3.19: ERR_BLOCKED_BY_CLIENT).
