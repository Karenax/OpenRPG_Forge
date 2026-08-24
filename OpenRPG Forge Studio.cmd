@echo off
setlocal
start "OpenRPG Forge Studio" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0scripts\Open-Studio.ps1"
endlocal
