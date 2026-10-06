# Cross Rhythm

[Play on TakKa note](https://takka-note.com/cross-rhythm/) · [Unity WebGL player](https://takka-d.github.io/cross-rhythm/) · [Legacy Web 0.3.13](https://takka-d.github.io/cross-rhythm/LegacyWeb/) · [Windows download](https://takka-d.github.io/cross-rhythm/download.html)

Unity 6 source is tracked in `UnityProject/`. The HTML/JavaScript version is tracked in `LegacyWeb/`.

## Established build flow

The canonical build flow is the existing Windows build script, not GameCI:

```powershell
cd UnityProject
.\Build.ps1 -Target Both
```

`Build.ps1` resolves Unity 6000.3.25f1 from either:

- `%ProgramFiles%\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe`
- `%USERPROFILE%\Documents\Codex\Unity\6000.3.25f1\Editor\Unity.exe`

or accepts an explicit `-UnityEditor` path.

Outputs:

- Windows: `UnityProject\Builds\Windows\CrossRhythm.exe`
- WebGL: `UnityProject\Builds\Web\`
- Logs: `UnityProject\Builds\Windows.log`, `UnityProject\Builds\Web.log`

The release process then publishes the WebGL build to the repository root/GitHub Pages and packages the Windows build under `Downloads/`.

Current source preview: 0.3.13.

The bundled Rhythm Check is a generated demo. Song recordings and user projects are not included.

Third-party notices and licenses are included in this repository.
