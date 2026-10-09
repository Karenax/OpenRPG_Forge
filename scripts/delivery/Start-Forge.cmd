@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Forge.ps1"
if errorlevel 1 pause
