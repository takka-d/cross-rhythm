$ErrorActionPreference='Stop'
$launcherRoot=$PSScriptRoot
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('cross-rhythm-launcher-'+[Guid]::NewGuid().ToString('N'))
$runtime=Join-Path $testRoot 'Runtime'
$launcher=Join-Path $testRoot 'CrossRhythm.exe'
$marker=Join-Path $testRoot 'player marker.txt'
$first=$null;$second=$null;$childId=0
try {
  New-Item -ItemType Directory -Force -Path $runtime | Out-Null
  & (Join-Path $launcherRoot 'Build-Launcher.ps1') -OutputPath $launcher
  $stub=Join-Path $testRoot 'PlayerStub.cs'
  @'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
class PlayerStub { static void Main(string[] args){File.WriteAllText(args[0],Process.GetCurrentProcess().Id.ToString());Thread.Sleep(60000);} }
'@ | Set-Content -LiteralPath $stub -Encoding UTF8
  $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  if(!(Test-Path $csc)){$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
  & $csc /nologo /target:winexe "/out:$(Join-Path $runtime 'CrossRhythmPlayer.exe')" $stub
  if($LASTEXITCODE -ne 0){throw 'Test player compilation failed.'}

  Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LauncherTestNative {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd,uint msg,IntPtr wParam,IntPtr lParam);
}
'@
  $watch=[Diagnostics.Stopwatch]::StartNew()
  $first=Start-Process -FilePath $launcher -ArgumentList ('"'+$marker+'"') -PassThru
  $window=[IntPtr]::Zero
  while($watch.Elapsed.TotalSeconds -lt 5 -and $window -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 25;$first.Refresh();$window=$first.MainWindowHandle}
  if($window -eq [IntPtr]::Zero){throw 'Startup window was not visible within five seconds.'}
  $visibleMs=$watch.ElapsedMilliseconds
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(!(Test-Path $marker) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
  if(!(Test-Path $marker)){throw 'Launcher did not start the child player.'}
  $childId=[int](Get-Content -LiteralPath $marker -Raw)

  $second=Start-Process -FilePath $launcher -ArgumentList ('"'+$marker+'"') -PassThru
  if(!$second.WaitForExit(3000)){throw 'Second launcher did not hand control to the existing instance.'}
  Start-Sleep -Milliseconds 200
  $players=@(Get-Process -Name CrossRhythmPlayer -ErrorAction SilentlyContinue | Where-Object {$_.Id -eq $childId})
  if($players.Count -ne 1){throw 'Second launcher created or lost the child player.'}

  [LauncherTestNative]::PostMessage($window,0x0100,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  [LauncherTestNative]::PostMessage($window,0x0101,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  if(!$first.WaitForExit(5000)){throw 'Escape did not close the launcher.'}
  Start-Sleep -Milliseconds 200
  if(Get-Process -Id $childId -ErrorAction SilentlyContinue){throw 'Escape did not terminate the child player.'}

  $source=Get-Content -LiteralPath (Join-Path $launcherRoot 'CrossRhythmLauncher.cs') -Raw
  if($source -notmatch 'close\.Click\+=\(s,e\)=>RequestCancel\(\)' -or $source -notmatch 'e\.KeyCode==Keys\.Escape.*RequestCancel\(\)'){throw 'Close and Escape must share the tested cancellation path.'}
  Write-Output "CROSS_RHYTHM_LAUNCHER_TEST_PASS visible_ms=$visibleMs duplicate_instances=1 escape_cancelled_child=true close_uses_same_cancel_path=true"
} finally {
  if($second -and !$second.HasExited){Stop-Process -Id $second.Id -Force -ErrorAction SilentlyContinue}
  if($first -and !$first.HasExited){Stop-Process -Id $first.Id -Force -ErrorAction SilentlyContinue}
  if($childId){Stop-Process -Id $childId -Force -ErrorAction SilentlyContinue}
  Start-Sleep -Milliseconds 100
  Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
