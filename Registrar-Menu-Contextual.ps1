# Script to register PDFWin in Windows 11 Context Menu & Startup
# Does not require Administrator privileges

$ErrorActionPreference = "SilentlyContinue"

$exePath = Join-Path $PSScriptRoot "PDFWin.exe"
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "PDFWin_App\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "publish\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path "$HOME\AppData\Local\Programs\PDFWin" "PDFWin.exe" }

if (-not (Test-Path $exePath)) {
    Write-Error "PDFWin.exe not found"
    exit 1
}

$appDir = [System.IO.Path]::GetDirectoryName($exePath)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Registering PDFWin Context Menu & Startup  " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Path: $appDir`n" -ForegroundColor Gray

# 1. Register Windows 11 Modern Context Menu (Sparse Package / IExplorerCommand)
$manifestPath = Join-Path $appDir "AppxManifest.xml"
if (-not (Test-Path $manifestPath)) {
    $manifestPath = Join-Path $PSScriptRoot "SparsePackage\AppxManifest.xml"
}

if (Test-Path $manifestPath) {
    Write-Host "[1/3] Registering Windows 11 modern shell extension..." -ForegroundColor Yellow
    try {
        $regDir = $appDir
        if ($appDir.StartsWith("F:\") -or $appDir.StartsWith("\\")) {
            $regDir = "$HOME\AppData\Local\Programs\PDFWin"
            if (-not (Test-Path $regDir)) {
                New-Item -ItemType Directory -Path $regDir -Force | Out-Null
                Copy-Item "$appDir\*" $regDir -Recurse -Force | Out-Null
            }
            $manifestPath = Join-Path $regDir "AppxManifest.xml"
        }

        Add-AppxPackage -Register $manifestPath -ExternalLocation $regDir -ForceApplicationShutdown
        Write-Host "  [✓] Windows 11 modern context menu activated!" -ForegroundColor Green
    } catch {
        Write-Warning "Could not register modern package: $($_.Exception.Message)"
    }
}

# 2. Register Startup entry in HKCU Run (Settings > Apps > Startup)
Write-Host "[2/3] Registering in Windows 11 Startup apps..." -ForegroundColor Yellow
try {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "PDFWin" -Value "`"$exePath`" --startup"
    Write-Host "  [✓] Registered in Settings > Apps > Startup!" -ForegroundColor Green
} catch {
    Write-Warning "Could not register startup key: $($_.Exception.Message)"
}

# 3. Register classic fallback associations in HKCU
Write-Host "[3/3] Registering classic registry associations..." -ForegroundColor Yellow
$menuTitle = "Create PDF"
$silentCmd = "`"$exePath`" --silent-create `"%1`""
$folderCmd = "`"$exePath`" --folder `"%1`""
$backgroundCmd = "`"$exePath`" --folder `"%V`""

$extensions = @("image", ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp")

foreach ($ext in $extensions) {
    $keyPath = "HKCU:\Software\Classes\SystemFileAssociations\$ext\shell\PDFWin.CreatePdf"
    New-Item -Path $keyPath -Force | Out-Null
    Set-ItemProperty -Path $keyPath -Name "(Default)" -Value $menuTitle
    Set-ItemProperty -Path $keyPath -Name "Icon" -Value $exePath

    $cmdKey = Join-Path $keyPath "command"
    New-Item -Path $cmdKey -Force | Out-Null
    Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value $silentCmd
}

# Directory
$folderKey = "HKCU:\Software\Classes\Directory\shell\PDFWin.CreatePdf"
New-Item -Path $folderKey -Force | Out-Null
Set-ItemProperty -Path $folderKey -Name "(Default)" -Value $menuTitle
Set-ItemProperty -Path $folderKey -Name "Icon" -Value $exePath
$folderCmdKey = Join-Path $folderKey "command"
New-Item -Path $folderCmdKey -Force | Out-Null
Set-ItemProperty -Path $folderCmdKey -Name "(Default)" -Value $folderCmd

# Directory Background
$bgKey = "HKCU:\Software\Classes\Directory\Background\shell\PDFWin.CreatePdf"
New-Item -Path $bgKey -Force | Out-Null
Set-ItemProperty -Path $bgKey -Name "(Default)" -Value $menuTitle
Set-ItemProperty -Path $bgKey -Name "Icon" -Value $exePath
$bgCmdKey = Join-Path $bgKey "command"
New-Item -Path $bgCmdKey -Force | Out-Null
Set-ItemProperty -Path $bgCmdKey -Name "(Default)" -Value $backgroundCmd

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "[✓] PDFWin context menu & startup registered successfully!" -ForegroundColor Green
Write-Host "Right-click on any images, PDFs or folders to see 'Create PDF'" -ForegroundColor White
Write-Host "directly in the modern Windows 11 context menu." -ForegroundColor White
Write-Host "============================================================`n" -ForegroundColor Cyan
