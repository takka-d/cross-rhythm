param([ValidateSet('Windows','Web','Both')][string]$Target='Both',[string]$UnityEditor='')
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -eq 'true' -and $env:CROSSRHYTHM_DEV_BUILD -ne '1') {
  throw 'Refusing GitHub Actions build without CROSSRHYTHM_DEV_BUILD=1. This prevents accidental production publishing.'
}
if (!$UnityEditor) {
  $documents=[Environment]::GetFolderPath('MyDocuments')
  $candidates=@((Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'),(Join-Path $documents 'Codex\Unity\6000.3.25f1\Editor\Unity.exe'))
  $UnityEditor=$candidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
}
if (!$UnityEditor -or !(Test-Path -LiteralPath $UnityEditor)) {throw 'Unity Editor was not found. Specify -UnityEditor with its Unity.exe path.'}
$targets=if($Target -eq 'Both'){@('Windows','Web')}else{@($Target)}
foreach($item in $targets) {
  if($item -eq 'Web' -and !(Test-Path -LiteralPath (Join-Path (Split-Path $UnityEditor -Parent) 'Data\PlaybackEngines\WebGLSupport'))) {throw 'Add Web Build Support for the selected Unity Editor.'}
  $output=if($item -eq 'Windows'){Join-Path $PSScriptRoot 'Builds\Windows\CrossRhythm.exe'}else{Join-Path $PSScriptRoot 'Builds\Web'}
  $log=Join-Path $PSScriptRoot ('Builds\'+$item+'.log')
  New-Item -ItemType Directory -Force -Path (Split-Path $log -Parent) | Out-Null
  $buildTarget=if($item -eq 'Web'){'WebGL'}else{'Win64'}
  $args=@('-batchmode','-nographics','-quit','-accept-apiupdate','-buildTarget',$buildTarget,'-projectPath',('"'+$PSScriptRoot+'"'),'-executeMethod',('CrossRhythm.CrossRhythmBuild.'+$item),'-buildOutput',('"'+$output+'"'),'-logFile',('"'+$log+'"'))
  $build=Start-Process -FilePath $UnityEditor -ArgumentList $args -WindowStyle Hidden -PassThru -Wait
  if($build.ExitCode -ne 0){throw ('Build failed. Read '+$log)}
  Write-Output ($item+': '+$output)
}
