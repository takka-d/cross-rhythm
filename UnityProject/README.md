# Cross Rhythm — Unity 0.3.13 Preview

Unity 6000.3.25f1 project for Cross Rhythm.

## 0.3.13
- Songs: selected project can be explicitly opened in Edit.
- Config: project folder uses the same read-only path + Open Folder row as Songs; Open Project was removed from Config.
- Practice: Back navigation, −0.05 / ×1 / +0.05 speed controls, and backing-audio pitch follows practice speed.
- Edit: File menu is in the top application header.
- ADTOF: WebGL and Windows can call the local companion at http://127.0.0.1:8765. Windows builds include Tools/ADTOF next to the executable and try to start the companion automatically.
- WebGL keeps the maximize/restore control; native mobile targets hide it.

## Build
Run `Build.ps1 -Target Both` on Windows with Unity 6000.3.25f1 installed, or pass `-UnityEditor` explicitly.

The Windows output receives an `ADTOF` folder. Run `ADTOF/install_adtof.bat` once if the model dependencies are not installed.
