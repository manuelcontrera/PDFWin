@echo off
title PDFWin - Registro de Menu Contextual Windows 11
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Registrar-Menu-Contextual.ps1"
echo.
pause
