Cross Rhythm / ADTOF companion

Windows: Edit > Analyze > Setup / Start opens Start ADTOF.cmd.
Web: download this companion, extract it, and run Start ADTOF.cmd.
Python 3.12 (https://www.python.org/downloads/) is needed for the first setup.
The first launch installs dependencies/model into %LOCALAPPDATA%/CrossRhythm/ADTOF.
Subsequent launches reuse them. The companion runs in the background at
http://127.0.0.1:8765. It is reachable only from this computer.
Return to Analyze, press Check, then ADTOF. Set BPM/offset in Song and Grid first.
Review the note count, then Apply (replace or append). Undo reverses Apply.
Cancel stops inference; the current chart is untouched until Apply.
Web browsers may request permission to connect to localhost. Allow it only if
you want to use this local companion. No permission or origin checks are bypassed.
Audio is processed locally; it is not sent to a cloud transcription service.
Temporary audio is removed after analysis or cancellation. Completed job records
are removed on the next job submission after ten minutes; logs are in
%LOCALAPPDATA%/CrossRhythm/ADTOF.

Detection: bass drum, snare, tom (MT), hi-hat, cymbal (CR).
Adjust tom/cymbal types, dynamics, open/closed hats and timing manually afterward.
ADTOF does not estimate song BPM or guarantee a complete/correct drum score.
Inference uses 30-second sections with 2-second context to limit memory.
Maximum input: 256 MB / 30 minutes. Auto Draft remains available without setup.

Model attribution and use:
ADTOF by M. Zehren and contributors: https://github.com/MZehren/ADTOF
Original repository/model: CC BY-NC-SA 4.0 (non-commercial / attribution / share alike).
License: https://github.com/MZehren/ADTOF/blob/master/LICENSE
PyTorch port by Xavier Riley: https://github.com/xavriley/ADTOF-pytorch
Pinned revision: 85c192e78f716ea0b111cc8a5ee4a8f6a3a4f8a9
The model and third-party implementation are not bundled with Cross Rhythm.
Setup downloads them from their source. Commercial use/redistribution needs
separate permission from the rights holders; installation does not grant it.

日本語:
初回はPython 3.12が必要です。Start ADTOF.cmdで必要な処理をインストールし、
起動後にEdit > Analyze > Check > ADTOFと進みます。解析結果はApplyを押すまで
譜面に反映されません。強弱・奏法・タムやシンバルの種類は手動で確認してください。
モデルは非商用条件です。広告を含む商用利用や再配布の許諾は別途必要です。
