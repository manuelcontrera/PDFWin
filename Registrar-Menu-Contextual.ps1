# Script para registrar PDFWin en el Menú Contextual de Windows 11 (Moderno y Clásico)
# No requiere permisos de Administrador

$ErrorActionPreference = "SilentlyContinue"

$exePath = Join-Path $PSScriptRoot "PDFWin.exe"
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "PDFWin_App\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path $PSScriptRoot "publish\PDFWin.exe" }
if (-not (Test-Path $exePath)) { $exePath = Join-Path "$HOME\AppData\Local\Programs\PDFWin" "PDFWin.exe" }

if (-not (Test-Path $exePath)) {
    Write-Error "No se encontró PDFWin.exe"
    exit 1
}

$appDir = [System.IO.Path]::GetDirectoryName($exePath)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Registrando PDFWin en el Menú Contextual   " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Ruta: $appDir`n" -ForegroundColor Gray

# 1. Registrar Menú Moderno de Windows 11 (Sparse Package / IExplorerCommand)
$manifestPath = Join-Path $appDir "AppxManifest.xml"
if (-not (Test-Path $manifestPath)) {
    $manifestPath = Join-Path $PSScriptRoot "SparsePackage\AppxManifest.xml"
}

if (Test-Path $manifestPath) {
    Write-Host "[1/2] Registrando extensión moderna de Windows 11..." -ForegroundColor Yellow
    try {
        # Si la ruta actual es una unidad virtual (ej. Google Drive F:\), usar copia local para AppXSvc
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
        Write-Host "  [✓] Menú principal moderno de Windows 11 activado!" -ForegroundColor Green
    } catch {
        Write-Warning "No se pudo registrar paquete moderno: $($_.Exception.Message)"
    }
}

# 2. Registrar asociaciones clásicas directas en HKCU (como fallback para Windows 10 y menú clásico)
Write-Host "[2/2] Registrando asociaciones clásicas del registro..." -ForegroundColor Yellow
$menuTitle = "Crear PDF con PDFWin"
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

# Carpetas
$folderKey = "HKCU:\Software\Classes\Directory\shell\PDFWin.CreatePdf"
New-Item -Path $folderKey -Force | Out-Null
Set-ItemProperty -Path $folderKey -Name "(Default)" -Value $menuTitle
Set-ItemProperty -Path $folderKey -Name "Icon" -Value $exePath
$folderCmdKey = Join-Path $folderKey "command"
New-Item -Path $folderCmdKey -Force | Out-Null
Set-ItemProperty -Path $folderCmdKey -Name "(Default)" -Value $folderCmd

# Fondo de carpeta
$bgKey = "HKCU:\Software\Classes\Directory\Background\shell\PDFWin.CreatePdf"
New-Item -Path $bgKey -Force | Out-Null
Set-ItemProperty -Path $bgKey -Name "(Default)" -Value "Crear PDF con imágenes de esta carpeta"
Set-ItemProperty -Path $bgKey -Name "Icon" -Value $exePath
$bgCmdKey = Join-Path $bgKey "command"
New-Item -Path $bgCmdKey -Force | Out-Null
Set-ItemProperty -Path $bgCmdKey -Name "(Default)" -Value $backgroundCmd

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "[✓] Menú contextual registrado exitosamente en Windows 11!" -ForegroundColor Green
Write-Host "Ahora aparece directamente en el menú principal moderno al hacer" -ForegroundColor White
Write-Host "clic derecho sobre imágenes, PDFs o carpetas." -ForegroundColor White
Write-Host "============================================================`n" -ForegroundColor Cyan
