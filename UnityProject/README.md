# Cross Rhythm 0.3.13 development

Unity 6000.3.25f1. Windows and WebGL use the same Assets/Scripts source.
Run `./Build.ps1 -Target Both` from PowerShell with the licensed Unity Editor
and its Web Build Support installed. Builds go to `Builds/Windows` and `Builds/Web`.
Tests run before BuildPipeline.BuildPlayer. No license activation workaround,
DLL swap or player version-byte replacement is part of this build.

The production root remains 0.3.12. Verified development builds are published
under `/dev/0.3.13/`. The WordPress article remains a draft.

Changes: common folder row in Songs/Config; Songs > Edit opens an independent
editable copy; File in Edit header; Back in Play/Practice; Practice speed
buttons (0.25–2); ADTOF local companion under Tools/ADTOF.

ADTOF: run Start ADTOF.cmd, then Edit > Analyze > Check > ADTOF.
Review the generated count and Apply; Undo reverses Apply. The model is
downloaded separately. See Tools/ADTOF/README.txt for setup and model license.
No personal song projects/audio are bundled.

LegacyWeb is the earlier HTML reference, not the current Unity implementation.
Its old ADTOF title did not correspond to an implemented analysis client.
