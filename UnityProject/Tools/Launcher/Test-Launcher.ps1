$ErrorActionPreference='Stop'
$launcherRoot=$PSScriptRoot
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('cross-rhythm-launcher-'+[Guid]::NewGuid().ToString('N'))
$runtime=Join-Path $testRoot 'Runtime'
$launcher=Join-Path $testRoot 'CrossRhythm.exe'
$cancelMarker=Join-Path $testRoot 'cancel player marker.txt'
$readyMarker=Join-Path $testRoot 'ready player marker.txt'
$cancelLauncher=$null;$duplicate=$null;$readyLauncher=$null;$readyDuplicate=$null
$cancelChildId=0;$readyChildId=0
try {
  New-Item -ItemType Directory -Force -Path $runtime | Out-Null
  & (Join-Path $launcherRoot 'Build-Launcher.ps1') -OutputPath $launcher
  $stub=Join-Path $testRoot 'PlayerStub.cs'
  @'
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
class PlayerStub {
  static string Value(string[] args,string name){int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:"";}
  static void Main(string[] args){
    File.WriteAllText(args[0],Process.GetCurrentProcess().Id.ToString());
    if(args.Contains("--signal-ready")){
      string status=Value(args,"--startup-status-file");
      if(status.Length>0)File.WriteAllText(status,"Restoring editor file",Encoding.UTF8);
      Thread.Sleep(900);
      using(var ready=EventWaitHandle.OpenExisting(Value(args,"--startup-ready-event")))ready.Set();
    }
    Thread.Sleep(60000);
  }
}
'@ | Set-Content -LiteralPath $stub -Encoding UTF8
  $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  if(!(Test-Path $csc)){$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
  & $csc /nologo /target:winexe "/out:$(Join-Path $runtime 'CrossRhythmPlayer.exe')" /reference:System.Core.dll $stub
  if($LASTEXITCODE -ne 0){throw 'Test player compilation failed.'}

  Add-Type -AssemblyName System.Windows.Forms
  Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LauncherTestNative {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd,uint msg,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd,int index);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd,out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

  # The startup surface must be visible immediately, borderless and monitor-sized.
  $watch=[Diagnostics.Stopwatch]::StartNew()
  $cancelLauncher=Start-Process -FilePath $launcher -ArgumentList ('"'+$cancelMarker+'"') -PassThru
  $cancelWindow=[IntPtr]::Zero
  while($watch.Elapsed.TotalSeconds -lt 5 -and $cancelWindow -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 25;$cancelLauncher.Refresh();$cancelWindow=$cancelLauncher.MainWindowHandle}
  if($cancelWindow -eq [IntPtr]::Zero){throw 'Startup surface was not visible within five seconds.'}
  $visibleMs=$watch.ElapsedMilliseconds
  $style=[LauncherTestNative]::GetWindowLong($cancelWindow,-16)
  if(($style -band 0x00c00000) -ne 0){throw 'Startup surface still has a captioned window frame.'}
  $rect=New-Object LauncherTestNative+RECT
  [LauncherTestNative]::GetWindowRect($cancelWindow,[ref]$rect) | Out-Null
  $screen=[Windows.Forms.Screen]::FromHandle($cancelWindow).Bounds
  if(($rect.Right-$rect.Left) -lt $screen.Width -or ($rect.Bottom-$rect.Top) -lt $screen.Height){throw 'Startup surface does not cover its monitor.'}

  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(!(Test-Path $cancelMarker) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
  if(!(Test-Path $cancelMarker)){throw 'Launcher did not start the child player.'}
  $cancelChildId=[int](Get-Content -LiteralPath $cancelMarker -Raw)

  $duplicate=Start-Process -FilePath $launcher -ArgumentList ('"'+$cancelMarker+'"') -PassThru
  if(!$duplicate.WaitForExit(3000)){throw 'Second launcher did not hand control to the startup surface.'}
  Start-Sleep -Milliseconds 200
  $players=@(Get-Process -Name CrossRhythmPlayer -ErrorAction SilentlyContinue | Where-Object {$_.Id -eq $cancelChildId})
  if($players.Count -ne 1){throw 'Second launcher created or lost the child player.'}

  [LauncherTestNative]::PostMessage($cancelWindow,0x0100,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  [LauncherTestNative]::PostMessage($cancelWindow,0x0101,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  if(!$cancelLauncher.WaitForExit(5000)){throw 'Escape did not close the launcher.'}
  Start-Sleep -Milliseconds 200
  if(Get-Process -Id $cancelChildId -ErrorAction SilentlyContinue){throw 'Escape did not terminate the child player.'}
  $cancelChildId=0

  # The same surface stays up through Unity startup, then hands off directly to
  # the ready player. No second launcher or second child is created meanwhile.
  $readyLauncher=Start-Process -FilePath $launcher -ArgumentList @('"'+$readyMarker+'"','--signal-ready') -PassThru
  $readyWindow=[IntPtr]::Zero
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while([DateTime]::UtcNow -lt $deadline -and $readyWindow -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 20;$readyLauncher.Refresh();$readyWindow=$readyLauncher.MainWindowHandle}
  if($readyWindow -eq [IntPtr]::Zero){throw 'Ready-handoff startup surface was never visible.'}
  while(!(Test-Path $readyMarker) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
  if(!(Test-Path $readyMarker)){throw 'Ready-handoff child did not start.'}
  $readyChildId=[int](Get-Content -LiteralPath $readyMarker -Raw)
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while([LauncherTestNative]::IsWindowVisible($readyWindow) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
  if([LauncherTestNative]::IsWindowVisible($readyWindow)){throw 'Startup surface remained visible after the player signalled readiness.'}

  $readyDuplicate=Start-Process -FilePath $launcher -ArgumentList @('"'+$readyMarker+'"','--signal-ready') -PassThru
  if(!$readyDuplicate.WaitForExit(3000)){throw 'Duplicate launch did not return to the ready player.'}
  $players=@(Get-Process -Name CrossRhythmPlayer -ErrorAction SilentlyContinue | Where-Object {$_.Id -eq $readyChildId})
  if($players.Count -ne 1){throw 'Ready handoff allowed a duplicate child player.'}
  Stop-Process -Id $readyChildId -Force
  $readyChildId=0
  if(!$readyLauncher.WaitForExit(5000)){throw 'Hidden launcher did not exit with the player.'}

  $source=Get-Content -LiteralPath (Join-Path $launcherRoot 'CrossRhythmLauncher.cs') -Raw
  if($source -notmatch 'FormBorderStyle=FormBorderStyle\.None' -or $source -notmatch 'AutoEllipsis=true'){throw 'Borderless and clipped-text safeguards are missing.'}
  if($source -notmatch 'close\.Click\+=\(s,e\)=>RequestCancel\(\)' -or $source -notmatch 'e\.KeyCode==Keys\.Escape.*RequestCancel\(\)'){throw 'Close and Escape must share the tested cancellation path.'}
  Write-Output "CROSS_RHYTHM_LAUNCHER_TEST_PASS visible_ms=$visibleMs borderless_full_monitor=true duplicate_instances=1 escape_cancelled_child=true ready_handoff=true status_bridge=true close_uses_same_cancel_path=true"
} finally {
  foreach($process in @($readyDuplicate,$readyLauncher,$duplicate,$cancelLauncher)){if($process -and !$process.HasExited){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}}
  foreach($id in @($readyChildId,$cancelChildId)){if($id){Stop-Process -Id $id -Force -ErrorAction SilentlyContinue}}
  Start-Sleep -Milliseconds 100
  if(Test-Path -LiteralPath $testRoot){
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(!$resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to remove a test directory outside the temporary folder.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
  }
}
