@echo off
setlocal
where py >nul 2>nul && (set PY=py -3) || (set PY=python)
%PY% "%~dp0cross_rhythm_transcriber_server.py"
if errorlevel 1 (
  echo.
  echo ADTOF server failed. Run install_adtof.bat first.
  pause
)
