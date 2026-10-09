@echo off
title PDFWin Installer - Windows 11
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-PDFWin.ps1"
echo.
pause
