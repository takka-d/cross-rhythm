$ErrorActionPreference = 'Stop'
$taskRuntime = Join-Path $env:LOCALAPPDATA 'CrossRhythm/ADTOF'
New-Item -ItemType Directory -Force -Path $taskRuntime | Out-Null
$taskPython = Join-Path $taskRuntime 'venv/Scripts/python.exe'
$taskPointer = Join-Path $taskRuntime 'python-path.txt'
if (Test-Path -LiteralPath $taskPointer) {
    $taskExisting = (Get-Content -LiteralPath $taskPointer -Raw).Trim()
    if (Test-Path -LiteralPath $taskExisting) { $taskPython = $taskExisting }
}
try {
    $taskHealth = Invoke-RestMethod 'http://127.0.0.1:8765/health' -TimeoutSec 2
    if ($taskHealth.service -eq 'cross-rhythm-adtof' -and $taskHealth.adtof_available) { Write-Host 'ADTOF is already running. Return to Cross Rhythm and press Check.'; exit 0 }
} catch {}
if (!(Test-Path -LiteralPath $taskPython)) {
    Write-Host 'First setup downloads Python packages and the ADTOF model (several hundred MB).'
    Write-Host 'Model: CC BY-NC-SA 4.0, non-commercial use. See README.txt for source/license.'
    $taskLauncher = Get-Command py -ErrorAction SilentlyContinue
    if (!$taskLauncher) { throw 'Install Python 3.12 from python.org, then run Start ADTOF again.' }
    & $taskLauncher.Source -3.12 -m venv (Join-Path $taskRuntime 'venv')
    if ($LASTEXITCODE -ne 0) { throw 'Python 3.12 is required for setup.' }
}
& $taskPython -c "import adtof_pytorch,torch,librosa"
if ($LASTEXITCODE -ne 0) {
    & $taskPython -m pip install 'torch==2.14.1+cpu' --index-url https://download.pytorch.org/whl/cpu
    if ($LASTEXITCODE -ne 0) { throw 'PyTorch installation failed.' }
    & $taskPython -m pip install 'librosa==1.0.0' 'numpy==2.5.3' 'pretty_midi==0.2.11.post0' 'https://github.com/xavriley/ADTOF-pytorch/archive/85c192e78f716ea0b111cc8a5ee4a8f6a3a4f8a9.zip'
    if ($LASTEXITCODE -ne 0) { throw 'ADTOF installation failed. See the error above.' }
}
$taskOut = Join-Path $taskRuntime 'server.log'
$taskErr = Join-Path $taskRuntime 'server-errors.log'
$taskServer = Join-Path $PSScriptRoot 'server.py'
$taskProcess = Start-Process -FilePath $taskPython -ArgumentList @(('"'+$taskServer+'"')) -WindowStyle Hidden -RedirectStandardOutput $taskOut -RedirectStandardError $taskErr -PassThru
$taskProcess.Id | Set-Content -LiteralPath (Join-Path $taskRuntime 'server.pid')
Write-Host 'ADTOF started locally. Return to Cross Rhythm and press Check, then ADTOF.'
Write-Host ('Log: '+$taskErr)
