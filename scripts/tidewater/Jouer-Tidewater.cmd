@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Walk-Tidewater.ps1" -SkipBuild
if errorlevel 1 pause
