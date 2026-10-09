@echo off
title PDFWin - Uninstall Context Menu & Startup
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Desinstalar-Menu-Contextual.ps1"
echo.
pause
