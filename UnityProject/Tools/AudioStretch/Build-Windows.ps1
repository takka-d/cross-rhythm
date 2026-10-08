param([string]$VisualStudio = 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=Join-Path $projectRoot 'Assets/Plugins/WebGL/CrossRhythmStretch.cpp'
$output=Join-Path $projectRoot 'Assets/Plugins/x86_64/CrossRhythmStretch.dll'
$buildDirectory=Join-Path $projectRoot 'Temp/AudioStretchNative'
New-Item -ItemType Directory -Force -Path (Split-Path $output),$buildDirectory | Out-Null
$vcvars=Join-Path $VisualStudio 'VC/Auxiliary/Build/vcvars64.bat'
$batch=Join-Path $buildDirectory 'compile.cmd'
@"
@echo off
call "$vcvars" >nul
if errorlevel 1 exit /b 1
cl /nologo /LD /O2 /MT /EHsc /std:c++14 /Fo"$buildDirectory\stretch.obj" /Fe"$output" "$source" /link /IMPLIB:"$buildDirectory\stretch.lib"
"@ | Set-Content -LiteralPath $batch -Encoding ascii
& $env:ComSpec /c $batch
if($LASTEXITCODE -ne 0){throw 'Audio stretch native build failed'}
Get-FileHash -LiteralPath $output -Algorithm SHA256
