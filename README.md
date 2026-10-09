# PDFWin - Aplicación Nativa para Windows 11 (WinUI 3)

**PDFWin** es una aplicación moderna, ultraligera y nativa para Windows 11 desarrollada con **WinUI 3**, **C# / .NET 8** y **C++ (Windows App SDK)**. Permite convertir y unir imágenes (JPG, PNG, WebP, TIFF, BMP) y documentos PDF en un único PDF optimizado con compresión visualmente sin pérdida (*visually lossless*).

Se integra de forma nativa directamente en el **menú contextual principal de Windows 11** ("Crear PDF con PDFWin"), sin necesidad de entrar a *"Show more options"*.

---

## Características Principales

1. **Integración Nativa en el Menú Principal de Windows 11**:
   - Implementa la interfaz COM moderna `IExplorerCommand` con un servidor COM out-of-process nativo en C++.
   - Registrado con identidad de paquete (*Sparse Package / External Location*).
   - Al hacer clic derecho sobre imágenes, documentos PDF o carpetas, **"Crear PDF con PDFWin"** aparece de inmediato en el **menú principal moderno** de Windows 11.
   - Procesa los archivos en segundo plano con notificación toast interactiva de Windows 11.

2. **Diseño Flotante y Moderno (WinUI 3 / Fluent Design)**:
   - Ventana flotante compacta con material translúcido *DesktopAcrylic*, bordes redondeados nativos del Gestor de Ventanas de Windows (DWM) e iconografía oficial *Segoe Fluent Icons*.
   - **Always-on-Top**: Se mantiene flotante mientras trabajas con otros programas.
   - **Memoria de posición**: Recuerda automáticamente tus coordenadas preferidas en pantalla.
   - Atajo de teclado global: **`Win + Alt + P`** para mostrar u ocultar la ventana en cualquier momento desde cualquier lugar.
   - Icono persistente en la **bandeja del sistema** (*System Tray*) junto al reloj.

3. **Drag & Drop Inteligente y Reordenamiento de Páginas**:
   - Arrastra y suelta imágenes o PDFs directamente en la tarjeta flotante.
   - **Orden natural alfanumérico inicial** (`StrCmpLogicalW`), preservando el orden real de las fotos (`foto_1`, `foto_2`, `foto_10`).
   - Reordena arrastrando miniaturas hacia arriba o abajo antes de exportar.

4. **Motor de Compresión y Calidad Visualmente Sin Pérdida**:
   - **JPEG / JPG**: Protocolo DCT Stream Pass-through directo (estilo `img2pdf`). Inyecta los bloques DCT comprimidos originales directamente en el PDF sin descomprimir ni recodificar. **0% de pérdida de calidad** y peso mínimo.
   - **PNG**: Inyección Flate lossless con canal alfa transparente preservado.
   - **WebP, TIFF, BMP**: Decodificación nativa acelerada con **WIC (Windows Imaging Component)**.
   - **Fusión de PDFs**: Clona los árboles de objetos y vectores de páginas manteniendo las fuentes, vectores y nitidez original.

---

## Instalación

### Método Recomendado (Instalador listo para usar)
1. Descarga el ejecutable **`PDFWin-Setup.exe`** desde la sección de **[Releases](https://github.com/manuelcontrera/PDFWin/releases)**.
2. Ejecuta el instalador.
3. En menos de 2 segundos, PDFWin quedará instalado en tu perfil de usuario (`%LOCALAPPDATA%\Programs\PDFWin`), con accesos directos en Escritorio y Menú Inicio, y el menú contextual moderno de Windows 11 activado.

---

## Cómo Usar

- **Desde el Menú Contextual:**
  1. Selecciona una o varias imágenes / PDFs en el Explorador de Windows (o haz clic derecho sobre una carpeta).
  2. Haz clic derecho y selecciona **"Crear PDF con PDFWin"**.
  3. PDFWin generará el archivo `Documento_Combinado.pdf` en segundo plano y te mostrará una notificación nativa con botón directo para abrirlo.

- **Desde la Ventana Flotante:**
  1. Presiona **`Win + Alt + P`** o abre el icono junto al reloj.
  2. Arrastra tus imágenes o PDFs a la ventana.
  3. Ajusta el orden si lo deseas y pulsa el botón de descarga (`\uE896`) en la esquina superior izquierda.

---

## Estructura del Proyecto

- `App.xaml` / `App.xaml.cs`: Inicialización de la aplicación WinUI 3 y ciclo de vida.
- `MainWindow.xaml` / `MainWindow.xaml.cs`: Tarjeta flotante interactiva WinUI 3 con acrylic y drag & drop.
- `Native/ShellExtension/`: Extensión COM nativa en C++ que implementa `IExplorerCommand` para el menú moderno de Windows 11.
- `SparsePackage/`: Manifiesto `AppxManifest.xml` y assets que otorgan identidad de paquete al ejecutable externo.
- `Services/PdfService.cs`: Motor de generación y fusión de PDF de alta fidelidad.
- `Services/SilentBatchManager.cs`: Coordinador de instancias concurrentes del menú contextual del Explorador.
- `Services/ContextMenuService.cs`: Registro y desregistro del menú contextual de Windows 11.
- `Services/TrayIconService.cs`: Icono y menú de la bandeja del sistema.
- `Services/ToastNotificationService.cs`: Notificaciones interactivas de Windows 11.
- `installer/`: Código fuente del instalador standalone autónomo (`PDFWin-Setup.exe`).

---

## Autor

Desarrollado por **Manuel Contrera**  
GitHub: [@manuelcontrera](https://github.com/manuelcontrera)
