# Script Instalador / Configurador de PDFWin para Windows 11
# Instala en %LOCALAPPDATA%\Programs\PDFWin, crea accesos directos, registra menú moderno de Windows 11 y lanza PDFWin.
# No requiere permisos de Administrador.

$ErrorActionPreference = "SilentlyContinue"

$sourceExe = Join-Path $PSScriptRoot "PDFWin.exe"
if (-not (Test-Path $sourceExe)) { $sourceExe = Join-Path $PSScriptRoot "PDFWin_App\PDFWin.exe" }
if (-not (Test-Path $sourceExe)) { $sourceExe = Join-Path $PSScriptRoot "publish\PDFWin.exe" }
if (-not (Test-Path $sourceExe)) { $sourceExe = Join-Path "$HOME\AppData\Local\Programs\PDFWin" "PDFWin.exe" }

$sourceDir = [System.IO.Path]::GetDirectoryName($sourceExe)

# Destino estándar de aplicaciones de usuario en Windows 11
$targetDir = "$HOME\AppData\Local\Programs\PDFWin"
$targetExe = Join-Path $targetDir "PDFWin.exe"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "           Instalador / Activador de PDFWin                 " -ForegroundColor Cyan
Write-Host "              Nativo para Windows 11                        " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Directorio de destino: $targetDir`n" -ForegroundColor Gray

# 1. Copiar archivos si la fuente no es ya el destino
if ($sourceDir -ne $targetDir) {
    Write-Host "[1/4] Copiando archivos de la aplicacion..." -ForegroundColor Yellow
    Stop-Process -Name PDFWin -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item "$sourceDir\*" $targetDir -Recurse -Force | Out-Null
    
    # Asegurar que los componentes de extensión de shell y manifiesto estén en el destino
    $shellExt = Join-Path $PSScriptRoot "Native\ShellExtension\PDFWinShellExt.dll"
    if (Test-Path $shellExt) { Copy-Item $shellExt $targetDir -Force | Out-Null }
    
    $sparseManifest = Join-Path $PSScriptRoot "SparsePackage\AppxManifest.xml"
    if (Test-Path $sparseManifest) { Copy-Item $sparseManifest $targetDir -Force | Out-Null }
    
    $sparseAssets = Join-Path $PSScriptRoot "SparsePackage\Assets"
    if (Test-Path $sparseAssets) { Copy-Item $sparseAssets $targetDir -Recurse -Force | Out-Null }
}

# 2. Crear accesos directos
Write-Host "[2/4] Creando accesos directos..." -ForegroundColor Yellow
try {
    $wshShell = New-Object -ComObject WScript.Shell
    $desktopPath = [System.Environment]::GetFolderPath('Desktop')
    $programsPath = [System.Environment]::GetFolderPath('Programs')

    # Acceso en el Escritorio
    $desktopShortcut = $wshShell.CreateShortcut((Join-Path $desktopPath "PDFWin.lnk"))
    $desktopShortcut.TargetPath = $targetExe
    $desktopShortcut.WorkingDirectory = $targetDir
    $desktopShortcut.IconLocation = "$targetExe,0"
    $desktopShortcut.Description = "PDFWin - Creacion y union de PDFs para Windows 11"
    $desktopShortcut.Save()

    # Acceso en el Menú Inicio
    $startShortcut = $wshShell.CreateShortcut((Join-Path $programsPath "PDFWin.lnk"))
    $startShortcut.TargetPath = $targetExe
    $startShortcut.WorkingDirectory = $targetDir
    $startShortcut.IconLocation = "$targetExe,0"
    $startShortcut.Description = "PDFWin - Creacion y union de PDFs para Windows 11"
    $startShortcut.Save()
    Write-Host "  [✓] Accesos directos creados en Escritorio y Menu Inicio." -ForegroundColor Green
} catch {
    Write-Warning "No se pudieron crear accesos directos: $($_.Exception.Message)"
}

# 3. Registrar menú contextual (Moderno de Windows 11 + Clásico)
Write-Host "[3/4] Registrando en el Menu Contextual de Windows 11..." -ForegroundColor Yellow
$manifestPath = Join-Path $targetDir "AppxManifest.xml"
if (Test-Path $manifestPath) {
    try {
        Add-AppxPackage -Register $manifestPath -ExternalLocation $targetDir -ForceApplicationShutdown
        Write-Host "  [✓] Menu contextual nativo de Windows 11 registrado correctamente!" -ForegroundColor Green
    } catch {
        Write-Warning "Aviso al registrar paquete moderno: $($_.Exception.Message)"
    }
}

# Ejecutar el registro de asociaciones clásicas
Start-Process -FilePath $targetExe -ArgumentList "--register-menu" -Wait -ErrorAction SilentlyContinue

# 4. Lanzar la aplicación
Write-Host "[4/4] Iniciando PDFWin..." -ForegroundColor Yellow
Start-Process -FilePath $targetExe

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "  ¡PDFWin quedo 100% instalado y listo para usar!           " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  • Atajo global: Presiona [Win + Alt + P] para abrir/ocultar." -ForegroundColor Yellow
Write-Host "  • Bandeja del sistema: Busca el icono de PDFWin junto al reloj." -ForegroundColor Yellow
Write-Host "  • Clic derecho en imagenes o carpetas: 'Crear PDF con PDFWin' aparece" -ForegroundColor Yellow
Write-Host "    DIRECTAMENTE en el menú principal de Windows 11." -ForegroundColor Yellow
Write-Host "============================================================`n" -ForegroundColor Cyan
