@echo off
title PDFWin - Desinstalar Menu Contextual Windows 11
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Desinstalar-Menu-Contextual.ps1"
echo.
pause
