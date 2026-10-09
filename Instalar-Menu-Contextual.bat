@echo off
title PDFWin - Windows 11 Context Menu & Startup Setup
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Registrar-Menu-Contextual.ps1"
echo.
pause
