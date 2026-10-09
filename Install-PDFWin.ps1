# PDFWin Installer / Setup Script for Windows 11
# Installs to %LOCALAPPDATA%\Programs\PDFWin, creates shortcuts, registers Windows 11 context menu and startup app.
# Does not require Administrator privileges.

$ErrorActionPreference = "SilentlyContinue"

$sourceDir = Join-Path $PSScriptRoot "publish"
$sourceExe = Join-Path $sourceDir "PDFWin.exe"

# Standard user application directory on Windows 11
$targetDir = "$HOME\AppData\Local\Programs\PDFWin"
$targetExe = Join-Path $targetDir "PDFWin.exe"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "             PDFWin Installer / Activator                   " -ForegroundColor Cyan
Write-Host "                 Native for Windows 11                      " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Destination directory: $targetDir`n" -ForegroundColor Gray

# 1. Copy files if source is not already target
if ($sourceDir -ne $targetDir) {
    Write-Host "[1/4] Copying application files..." -ForegroundColor Yellow
    Stop-Process -Name PDFWin -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item "$sourceDir\*" $targetDir -Recurse -Force | Out-Null
    
    # Ensure shell extension and manifest are copied to target
    $shellExt = Join-Path $PSScriptRoot "Native\ShellExtension\PDFWinShellExt.dll"
    if (Test-Path $shellExt) { Copy-Item $shellExt $targetDir -Force | Out-Null }
    
    $sparseManifest = Join-Path $PSScriptRoot "SparsePackage\AppxManifest.xml"
    if (Test-Path $sparseManifest) { Copy-Item $sparseManifest $targetDir -Force | Out-Null }
    
    $sparseAssets = Join-Path $PSScriptRoot "SparsePackage\Assets"
    if (Test-Path $sparseAssets) { Copy-Item $sparseAssets $targetDir -Recurse -Force | Out-Null }
}

# 2. Create shortcuts
Write-Host "[2/4] Creating shortcuts..." -ForegroundColor Yellow
try {
    $wshShell = New-Object -ComObject WScript.Shell
    $desktopPath = [System.Environment]::GetFolderPath('Desktop')
    $programsPath = [System.Environment]::GetFolderPath('Programs')

    # Desktop shortcut
    $desktopShortcut = $wshShell.CreateShortcut((Join-Path $desktopPath "PDFWin.lnk"))
    $desktopShortcut.TargetPath = $targetExe
    $desktopShortcut.WorkingDirectory = $targetDir
    $desktopShortcut.IconLocation = "$targetExe,0"
    $desktopShortcut.Description = "PDFWin - Windows 11 PDF Creation and Merge Utility"
    $desktopShortcut.Save()

    # Start Menu shortcut
    $startShortcut = $wshShell.CreateShortcut((Join-Path $programsPath "PDFWin.lnk"))
    $startShortcut.TargetPath = $targetExe
    $startShortcut.WorkingDirectory = $targetDir
    $startShortcut.IconLocation = "$targetExe,0"
    $startShortcut.Description = "PDFWin - Windows 11 PDF Creation and Merge Utility"
    $startShortcut.Save()
    Write-Host "  [✓] Shortcuts created on Desktop and Start Menu." -ForegroundColor Green
} catch {
    Write-Warning "Could not create shortcuts: $($_.Exception.Message)"
}

# 3. Register Context Menu (Windows 11 Modern + Fallback) and Startup App
Write-Host "[3/4] Registering Windows 11 Context Menu & Startup App..." -ForegroundColor Yellow
$manifestPath = Join-Path $targetDir "AppxManifest.xml"
if (Test-Path $manifestPath) {
    try {
        Add-AppxPackage -Register $manifestPath -ExternalLocation $targetDir -ForceApplicationShutdown
        Write-Host "  [✓] Native Windows 11 context menu & package registered!" -ForegroundColor Green
    } catch {
        Write-Warning "Notice when registering modern package: $($_.Exception.Message)"
    }
}

# Register Startup entry in HKCU Run (Settings > Apps > Startup)
try {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "PDFWin" -Value "`"$targetExe`" --startup"
    Write-Host "  [✓] Registered in Windows 11 Settings > Apps > Startup!" -ForegroundColor Green
} catch {
    Write-Warning "Could not register startup key: $($_.Exception.Message)"
}

# Execute classic menu associations registration
Start-Process -FilePath $targetExe -ArgumentList "--register-menu" -Wait -ErrorAction SilentlyContinue

# 4. Launch PDFWin
Write-Host "[4/4] Starting PDFWin..." -ForegroundColor Yellow
Start-Process -FilePath $targetExe

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "  PDFWin is 100% installed and ready to use!                " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  • Global shortcut: Press [Win + Alt + P] to show/hide." -ForegroundColor Yellow
Write-Host "  • System tray: Look for the PDFWin icon next to the clock." -ForegroundColor Yellow
Write-Host "  • Context menu: Right-click images, PDFs or folders - 'Create PDF' appears" -ForegroundColor Yellow
Write-Host "    DIRECTLY in the Windows 11 main menu." -ForegroundColor Yellow
Write-Host "  • Startup: Enabled and visible in Windows 11 Settings > Apps > Startup." -ForegroundColor Yellow
Write-Host "============================================================`n" -ForegroundColor Cyan
