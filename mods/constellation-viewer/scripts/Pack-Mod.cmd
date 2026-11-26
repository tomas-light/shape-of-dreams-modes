@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Pack-Mod.ps1" %*
exit /b %ERRORLEVEL%
