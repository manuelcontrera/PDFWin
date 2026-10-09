# Script to uninstall PDFWin from Windows 11 Context Menu & Startup
$ErrorActionPreference = "SilentlyContinue"

Write-Host "Uninstalling PDFWin context menu & startup registration..." -ForegroundColor Cyan

# 1. Unregister Windows 11 modern package
Get-AppxPackage -Name "*PDFWin*" | Remove-AppxPackage -ErrorAction SilentlyContinue

# 2. Unregister Startup entry
try {
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "PDFWin" -ErrorAction SilentlyContinue
} catch { }

# 3. Unregister classic associations
$exePath = Join-Path $PSScriptRoot "PDFWin.exe"
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "PDFWin_App\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "publish\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path "$HOME\AppData\Local\Programs\PDFWin" "PDFWin.exe" }

if (Test-Path $exePath) {
    Start-Process -FilePath $exePath -ArgumentList "--unregister-menu" -Wait -ErrorAction SilentlyContinue
}

$extensions = @("image", ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp")

foreach ($ext in $extensions) {
    $keyPath = "HKCU:\Software\Classes\SystemFileAssociations\$ext\shell\PDFWin.CreatePdf"
    if (Test-Path $keyPath) {
        Remove-Item -Path $keyPath -Recurse -Force
    }
}

$folderKey = "HKCU:\Software\Classes\Directory\shell\PDFWin.CreatePdf"
if (Test-Path $folderKey) {
    Remove-Item -Path $folderKey -Recurse -Force
}

$bgKey = "HKCU:\Software\Classes\Directory\Background\shell\PDFWin.CreatePdf"
if (Test-Path $bgKey) {
    Remove-Item -Path $bgKey -Recurse -Force
}

Write-Host "[✓] PDFWin context menu & startup uninstalled successfully." -ForegroundColor Green
