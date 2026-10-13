param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$launcherRoot=$PSScriptRoot
$projectRoot=(Resolve-Path (Join-Path $launcherRoot '..\..')).Path
$source=Join-Path $launcherRoot 'CrossRhythmLauncher.cs'
$manifest=Join-Path $launcherRoot 'CrossRhythmLauncher.manifest'
$icon=Join-Path $launcherRoot 'CrossRhythm.ico'
$compilerCandidates=@(
  (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
  (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler=$compilerCandidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
if(!$compiler){throw '.NET Framework C# compiler was not found.'}
foreach($required in @($source,$manifest,$icon)){if(!(Test-Path -LiteralPath $required)){throw "Launcher input is missing: $required"}}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',"/out:$OutputPath",'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',"/win32icon:$icon", "/win32manifest:$manifest",$source)
$messages=& $compiler @arguments 2>&1
if($LASTEXITCODE -ne 0){throw ($messages -join [Environment]::NewLine)}
$bytes=[IO.File]::ReadAllBytes($OutputPath)
if($bytes.Length -lt 1024 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a){throw 'Launcher compiler did not produce a valid Windows executable.'}
Write-Output "CROSS_RHYTHM_LAUNCHER_BUILD_PASS $OutputPath"
