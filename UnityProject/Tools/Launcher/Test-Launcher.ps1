$ErrorActionPreference='Stop'
$launcherRoot=$PSScriptRoot
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('cross-rhythm-launcher-'+[Guid]::NewGuid().ToString('N'))
$runtime=Join-Path $testRoot 'Runtime'
$launcher=Join-Path $testRoot 'CrossRhythm.exe'
$foreignRoot=Join-Path $testRoot 'foreign-copy'
$foreignRuntime=Join-Path $foreignRoot 'Runtime'
$foreignLauncher=Join-Path $foreignRoot 'CrossRhythm.exe'
$foreignMarker=Join-Path $testRoot 'foreign player marker.txt'
$closeMarker=Join-Path $testRoot 'close player marker.txt'
$escapeMarker=Join-Path $testRoot 'escape player marker.txt'
$readyMarker=Join-Path $testRoot 'ready player marker.txt'
$foreignLauncherProcess=$null;$closeLauncher=$null;$duplicate=$null;$escapeLauncher=$null;$readyLauncher=$null;$readyDuplicate=$null
$foreignChildId=0;$closeChildId=0;$escapeChildId=0;$readyChildId=0
try {
  New-Item -ItemType Directory -Force -Path $runtime,$foreignRuntime | Out-Null
  & (Join-Path $launcherRoot 'Build-Launcher.ps1') -OutputPath $launcher
  & (Join-Path $launcherRoot 'Build-Launcher.ps1') -OutputPath $foreignLauncher
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
      Thread.Sleep(1100);
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
  Copy-Item -LiteralPath (Join-Path $runtime 'CrossRhythmPlayer.exe') -Destination (Join-Path $foreignRuntime 'CrossRhythmPlayer.exe')

  Add-Type -AssemblyName System.Windows.Forms
  Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class LauncherTestNative {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public delegate bool EnumWindowProc(IntPtr window,IntPtr parameter);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd,uint msg,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd,int index);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd,out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd,int x,int y,int width,int height,bool repaint);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent,EnumWindowProc callback,IntPtr parameter);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int capacity);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
  public static string ReadText(IntPtr window){var text=new StringBuilder(512);GetWindowText(window,text,text.Capacity);return text.ToString();}
  public static IntPtr FindChildByText(IntPtr parent,string expected){
    IntPtr found=IntPtr.Zero;
    EnumChildWindows(parent,delegate(IntPtr window,IntPtr parameter){if(ReadText(window)==expected){found=window;return false;}return true;},IntPtr.Zero);
    return found;
  }
}
'@

  function Wait-LauncherWindow([Diagnostics.Process]$process,[int]$seconds=5){
    $deadline=[DateTime]::UtcNow.AddSeconds($seconds);$window=[IntPtr]::Zero
    while([DateTime]::UtcNow -lt $deadline -and $window -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 20;$process.Refresh();$window=$process.MainWindowHandle}
    if($window -eq [IntPtr]::Zero){throw 'Startup surface was not visible within five seconds.'}
    return $window
  }
  function Wait-Marker([string]$path,[string]$failure){
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while(!(Test-Path -LiteralPath $path) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 20}
    if(!(Test-Path -LiteralPath $path)){throw $failure}
    return [int](Get-Content -LiteralPath $path -Raw)
  }
  function Get-Rect([IntPtr]$window){
    $rect=New-Object LauncherTestNative+RECT
    if(![LauncherTestNative]::GetWindowRect($window,[ref]$rect)){throw 'Could not read a launcher control rectangle.'}
    return $rect
  }
  function Assert-Child([IntPtr]$parent,[string]$text){
    $window=[LauncherTestNative]::FindChildByText($parent,$text)
    if($window -eq [IntPtr]::Zero){throw "Launcher child was not rendered: $text"}
    return $window
  }
  function Test-Overlap($first,$second){
    return $first.Left -lt $second.Right -and $first.Right -gt $second.Left -and $first.Top -lt $second.Bottom -and $first.Bottom -gt $second.Top
  }

  # A player and launcher from another extracted copy must not suppress this
  # package's startup surface or receive this package's ready signal.
  $foreignLauncherProcess=Start-Process -FilePath $foreignLauncher -ArgumentList ('"'+$foreignMarker+'"') -PassThru
  $foreignWindow=Wait-LauncherWindow $foreignLauncherProcess
  $foreignChildId=Wait-Marker $foreignMarker 'Foreign package did not start its child player.'

  # The startup surface is a centered fixed-ratio game panel instead of a
  # full-monitor overlay. Validate the actual native window at the active DPI.
  $watch=[Diagnostics.Stopwatch]::StartNew()
  $closeLauncher=Start-Process -FilePath $launcher -ArgumentList ('"'+$closeMarker+'"') -PassThru
  $closeWindow=Wait-LauncherWindow $closeLauncher
  $visibleMs=$watch.ElapsedMilliseconds
  $style=[LauncherTestNative]::GetWindowLong($closeWindow,-16)
  if(($style -band 0x00c00000) -ne 0){throw 'Startup surface still has a captioned Windows frame.'}
  $rect=Get-Rect $closeWindow
  $screen=[Windows.Forms.Screen]::FromHandle($closeWindow)
  $width=$rect.Right-$rect.Left;$height=$rect.Bottom-$rect.Top
  if($width -ge [Math]::Floor($screen.Bounds.Width*0.95) -or $height -ge [Math]::Floor($screen.Bounds.Height*0.95)){throw 'Startup surface is still effectively full-screen.'}
  if($width -gt [Math]::Ceiling($screen.WorkingArea.Width*0.88) -or $height -gt [Math]::Ceiling($screen.WorkingArea.Height*0.82)){throw 'Startup surface exceeds its bounded working-area size.'}
  if([Math]::Abs(($width/[double]$height)-(820/461)) -gt 0.025){throw 'Startup surface did not preserve its 16:9 presentation ratio.'}
  $expectedLeft=$screen.WorkingArea.Left+[Math]::Floor(($screen.WorkingArea.Width-$width)/2)
  $expectedTop=$screen.WorkingArea.Top+[Math]::Floor(($screen.WorkingArea.Height-$height)/2)
  if([Math]::Abs($rect.Left-$expectedLeft) -gt 3 -or [Math]::Abs($rect.Top-$expectedTop) -gt 3){throw 'Startup surface is not centered in the monitor working area.'}
  $dpi=[LauncherTestNative]::GetDpiForWindow($closeWindow);if($dpi -lt 96){$dpi=96}
  $logicalWidth=$width*96/[double]$dpi;$logicalHeight=$height*96/[double]$dpi
  if($logicalWidth -lt 790 -or $logicalWidth -gt 850 -or $logicalHeight -lt 435 -or $logicalHeight -gt 485){throw "Startup surface is not DPI-scaled from its intended fixed size: ${logicalWidth}x${logicalHeight}."}

  $titleWindow=Assert-Child $closeWindow 'CROSS RHYTHM'
  $headingWindow=[LauncherTestNative]::FindChildByText($closeWindow,'Cross Rhythm を起動しています')
  if($headingWindow -eq [IntPtr]::Zero){$headingWindow=Assert-Child $closeWindow 'Starting Cross Rhythm'}
  $detailWindow=[LauncherTestNative]::FindChildByText($closeWindow,'ゲーム画面の準備を開始します')
  if($detailWindow -eq [IntPtr]::Zero){$detailWindow=Assert-Child $closeWindow 'Preparing the game screen'}
  $closeButton=Assert-Child $closeWindow '×'
  $titleRect=Get-Rect $titleWindow;$headingRect=Get-Rect $headingWindow;$detailRect=Get-Rect $detailWindow;$closeRect=Get-Rect $closeButton
  if($titleRect.Bottom -gt $headingRect.Top -or $headingRect.Bottom -gt $detailRect.Top){throw 'Central launcher text regions overlap.'}
  if($titleRect.Left -lt $rect.Left -or $titleRect.Right -gt $rect.Right -or $detailRect.Bottom -gt $rect.Bottom){throw 'Central launcher text is clipped outside the startup surface.'}
  if(($closeRect.Right-$closeRect.Left) -lt [Math]::Floor(38*$dpi/96) -or ($closeRect.Bottom-$closeRect.Top) -lt [Math]::Floor(38*$dpi/96)){throw 'The × Close control has an inadequate hit area at the active DPI.'}
  if(Test-Overlap $titleRect $closeRect){throw 'Close overlaps the title at the preferred startup size.'}

  # Force a compact 16:9 client to cover the small-monitor fallback layout. The
  # fixed surface normally chooses this size only when the work area requires it.
  $compactWidth=[Math]::Round(600*$dpi/96);$compactHeight=[Math]::Round(338*$dpi/96)
  if(![LauncherTestNative]::MoveWindow($closeWindow,$rect.Left,$rect.Top,$compactWidth,$compactHeight,$true)){throw 'Could not exercise compact launcher layout.'}
  Start-Sleep -Milliseconds 120
  $compactRect=Get-Rect $closeWindow;$titleRect=Get-Rect $titleWindow;$headingRect=Get-Rect $headingWindow;$detailRect=Get-Rect $detailWindow;$closeRect=Get-Rect $closeButton
  if($titleRect.Bottom -gt $headingRect.Top -or $headingRect.Bottom -gt $detailRect.Top -or (Test-Overlap $titleRect $closeRect)){throw 'Compact launcher controls overlap.'}
  if($titleRect.Left -lt $compactRect.Left -or $titleRect.Right -gt $compactRect.Right -or $detailRect.Bottom -gt $compactRect.Bottom){throw 'Compact launcher text is clipped outside the startup surface.'}

  $closeChildId=Wait-Marker $closeMarker 'Launcher did not start the child player.'
  if($closeChildId -eq $foreignChildId){throw 'The launcher attached to a different package solely because the player names matched.'}
  if($foreignLauncherProcess.HasExited){throw 'Starting another extracted copy displaced the original launcher.'}
  Stop-Process -Id $foreignLauncherProcess.Id -Force
  $foreignLauncherProcess=$null
  Stop-Process -Id $foreignChildId -Force
  $foreignChildId=0
  $duplicate=Start-Process -FilePath $launcher -ArgumentList ('"'+$closeMarker+'"') -PassThru
  if(!$duplicate.WaitForExit(3000)){throw 'Second launcher did not hand control to the startup surface.'}
  Start-Sleep -Milliseconds 150
  $players=@(Get-Process -Name CrossRhythmPlayer -ErrorAction SilentlyContinue | Where-Object {$_.Id -eq $closeChildId})
  if($players.Count -ne 1){throw 'Second launcher created or lost the child player.'}

  # Exercise the real native button, not only the shared handler source text.
  [LauncherTestNative]::PostMessage($closeButton,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
  if(!$closeLauncher.WaitForExit(5000)){throw 'Clicking Close did not close the launcher.'}
  Start-Sleep -Milliseconds 150
  if(Get-Process -Id $closeChildId -ErrorAction SilentlyContinue){throw 'Clicking Close did not terminate the child player.'}
  $closeChildId=0

  # Escape remains a separate route into the same cancellation behaviour.
  $escapeLauncher=Start-Process -FilePath $launcher -ArgumentList ('"'+$escapeMarker+'"') -PassThru
  $escapeWindow=Wait-LauncherWindow $escapeLauncher
  $escapeChildId=Wait-Marker $escapeMarker 'Escape test child did not start.'
  [LauncherTestNative]::PostMessage($escapeWindow,0x0100,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  [LauncherTestNative]::PostMessage($escapeWindow,0x0101,[IntPtr]0x1b,[IntPtr]::Zero) | Out-Null
  if(!$escapeLauncher.WaitForExit(5000)){throw 'Escape did not close the launcher.'}
  Start-Sleep -Milliseconds 150
  if(Get-Process -Id $escapeChildId -ErrorAction SilentlyContinue){throw 'Escape did not terminate the child player.'}
  $escapeChildId=0

  # The same surface displays the restore state, then hands off directly to the
  # ready player. No second startup screen or second child is created meanwhile.
  $readyLauncher=Start-Process -FilePath $launcher -ArgumentList @('"'+$readyMarker+'"','--signal-ready') -PassThru
  $readyWindow=Wait-LauncherWindow $readyLauncher
  $readyChildId=Wait-Marker $readyMarker 'Ready-handoff child did not start.'
  $deadline=[DateTime]::UtcNow.AddSeconds(3);$statusWindow=[IntPtr]::Zero
  while([DateTime]::UtcNow -lt $deadline -and $statusWindow -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 20;$statusWindow=[LauncherTestNative]::FindChildByText($readyWindow,'Restoring editor file')}
  if($statusWindow -eq [IntPtr]::Zero){throw 'Restore status was not shown on the existing startup surface.'}
  if($readyWindow -ne $readyLauncher.MainWindowHandle){throw 'Restore status replaced the startup surface instead of using the same window.'}
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
  $manifest=Get-Content -LiteralPath (Join-Path $launcherRoot 'CrossRhythmLauncher.manifest') -Raw
  if($source -notmatch 'AutoScaleMode=AutoScaleMode\.Dpi' -or $source -notmatch 'WM_DPICHANGED'){throw 'Per-monitor DPI layout safeguards are missing.'}
  if($source -notmatch 'GameCloseButton' -or $source -notmatch 'close\.Click\+=\(s,e\)=>RequestCancel\(\)' -or $source -notmatch 'e\.KeyCode==Keys\.Escape.*RequestCancel\(\)'){throw 'Close and Escape must share the tested cancellation path.'}
  if($source -notmatch 'PackageKey\(AppDomain\.CurrentDomain\.BaseDirectory\)' -or $source -match 'TryActivatePlayer'){throw 'Launcher identity is not scoped to the extracted package.'}
  if($source -notmatch 'visiblePaints>0.*StartPlayer\(\)' -or $source -match 'Shown\+=.*StartPlayer\(\)'){throw 'The player may start before the startup surface has painted.'}
  if($source -match 'AutoEllipsis=true' -or $source -match 'EndEllipsis' -or $source -match 'Text="CLOSE"' -or $source -match 'Text=Japanese\(\)\?"閉じる"' -or $source -match 'iconSize=.*DrawLine'){throw 'Startup text or × Close still uses abbreviation or redundant caption decoration.'}
  if($manifest -notmatch 'PerMonitorV2,PerMonitor' -or $manifest -notmatch 'true/pm'){throw 'The launcher manifest is not per-monitor DPI aware.'}
  Write-Output "CROSS_RHYTHM_LAUNCHER_TEST_PASS visible_ms=$visibleMs centered_fixed_ratio=true fullscreen=false dpi=$dpi nonoverlapping_text=true full_text=true clean_close=true close_cancelled_child=true escape_cancelled_child=true duplicate_instances=1 package_isolation=true painted_before_player=true ready_handoff=true status_bridge=true one_startup_surface=true"
} finally {
  foreach($process in @($readyDuplicate,$readyLauncher,$escapeLauncher,$duplicate,$closeLauncher,$foreignLauncherProcess)){if($process -and !$process.HasExited){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}}
  foreach($childProcessId in @($readyChildId,$escapeChildId,$closeChildId,$foreignChildId)){if($childProcessId){Stop-Process -Id $childProcessId -Force -ErrorAction SilentlyContinue}}
  Start-Sleep -Milliseconds 100
  if(Test-Path -LiteralPath $testRoot){
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(!$resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to remove a test directory outside the temporary folder.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
  }
}
