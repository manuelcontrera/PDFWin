@echo off
title PDFWin - Uninstall Context Menu & Startup
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-PDFWin.ps1"
echo.
pause
