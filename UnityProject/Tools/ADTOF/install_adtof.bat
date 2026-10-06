@echo off
setlocal
where py >nul 2>nul && (set PY=py -3) || (set PY=python)
%PY% -m pip install --upgrade -r "%~dp0requirements.txt"
if errorlevel 1 pause
