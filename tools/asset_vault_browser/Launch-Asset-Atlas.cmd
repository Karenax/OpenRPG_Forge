@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launch-Asset-Atlas.ps1" %*
if errorlevel 1 pause
endlocal
