@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Test
if errorlevel 1 pause
if not errorlevel 1 explorer.exe /select,"%~dp0dist\PillowcaseLinkConverter.exe"
