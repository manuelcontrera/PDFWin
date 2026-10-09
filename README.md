# PDFWin - Native Windows 11 Application (WinUI 3)

**PDFWin** is a modern, ultra-lightweight, native Windows 11 utility built with **WinUI 3**, **C# / .NET 8**, and **C++ (Windows App SDK)**. It effortlessly converts and merges images (JPG, PNG, WebP, TIFF, BMP) and PDF documents into a single optimized, visually lossless PDF.

It integrates natively into the **Windows 11 modern context menu** as **"Create PDF"**, without having to click *"Show more options"*.

---

## Key Features

1. **Native Windows 11 Context Menu Integration ("Create PDF")**:
   - Implements the modern `IExplorerCommand` COM interface via a high-performance native C++ shell extension.
   - Registered with Windows package identity (*Sparse Package / External Location*).
   - Right-click any image, PDF file, multiple selections, or folders and **"Create PDF"** appears directly in the modern Windows 11 top-level menu.
   - Processes conversions silently in the background with native Windows 11 interactive toast notifications.

2. **Windows 11 Startup Apps Support (`Settings > Apps > Startup`)**:
   - Registered as an official Windows 11 startup application.
   - Easily toggled On/Off directly inside **Windows 11 Settings > Apps > Startup** or Task Manager.
   - When launched at startup, it runs quietly in the system tray without intruding on screen, ready to respond instantly.

3. **Floating Card Design (WinUI 3 / Fluent Design)**:
   - Compact floating card window featuring native Windows 11 rounded corners (DWM), Mica Alt backdrop, and official Segoe Fluent Icons.
   - **Always-on-Top**: Floats above your workspace while you drag and organize files from different windows.
   - **Position Memory**: Automatically remembers and restores your preferred screen coordinates.
   - **Global Hotkey**: Press **`Win + Alt + P`** from any application to show or hide the floating window instantly.
   - **System Tray Icon**: Persistent icon in the notification area next to the taskbar clock with quick access controls.

4. **Smart Drag & Drop with Page Reordering**:
   - Drag and drop images or PDFs directly onto the floating card.
   - **Natural Alphanumeric Sort** (`StrCmpLogicalW`) by default, preserving real numerical file ordering (`photo_1`, `photo_2`, `photo_10`).
   - Visually reorder pages by dragging thumbnails before exporting.
   - Quick flyout actions to sort alphabetically or clear all items.

5. **Visually Lossless Engine**:
   - **JPEG / JPG**: DCT Stream Pass-through protocol (similar to `img2pdf`). Embeds the original compressed DCT byte blocks directly into the PDF without decoding or re-encoding. **0% generation loss** with minimal file size.
   - **PNG**: Lossless Flate injection preserving transparent alpha channels.
   - **WebP, TIFF, BMP**: Hardware-accelerated decoding via native **WIC (Windows Imaging Component)**.
   - **PDF Merging**: Directly clones PDF object trees and vectors, maintaining crystal-clear vectors, fonts, and sharpness.

---

## Installation

### Recommended Method (Standalone Installer)
1. Download **`PDFWin-Setup.exe`** from the **[Releases](https://github.com/manuelcontrera/PDFWin/releases)** page.
2. Run the installer.
3. In seconds, PDFWin will be installed to your user profile (`%LOCALAPPDATA%\Programs\PDFWin`), shortcuts will be created on your Desktop and Start Menu, and both the modern context menu ("Create PDF") and Startup Apps entry will be registered.

### Portable / Script Method
1. Clone or download the repository.
2. Run `Instalar-PDFWin.bat` (or execute `Instalar-PDFWin.ps1` in PowerShell).
3. The script automatically sets up the local application directory, registers the sparse package, context menu, and startup keys.

---

## How to Use

- **From Windows Explorer Context Menu:**
  1. Select one or more images/PDFs (or right-click a folder).
  2. Right-click and choose **"Create PDF"**.
  3. PDFWin processes the files silently in the background and displays a notification with a button to open the generated PDF or containing folder.

- **From the Floating Window:**
  1. Press **`Win + Alt + P`** or click the tray icon next to the clock.
  2. Drag and drop your images or PDFs into the card.
  3. Reorder items as needed and click the Create PDF button (`\uE896`) in the top-left corner.

---

## Command-Line Arguments

| Argument | Description |
| :--- | :--- |
| `--startup` / `--minimized` | Starts PDFWin silently in the system tray without showing the main window. |
| `--silent-create <files...>` | Silently creates a combined PDF from the provided file paths and triggers a toast notification. |
| `--folder <folder_path>` | Converts all supported images and PDFs found in the specified folder. |
| `--register-menu` | Registers both modern and classic Explorer context menu associations and startup registry. |
| `--unregister-menu` | Unregisters Explorer context menu associations and startup registry. |
| `--register-startup` | Registers PDFWin in Windows 11 Startup apps. |
| `--unregister-startup` | Removes PDFWin from Windows 11 Startup apps. |

---

## Project Structure

- `App.xaml` / `App.xaml.cs`: WinUI 3 application lifecycle, startup detection, and single-instance coordination.
- `MainWindow.xaml` / `MainWindow.xaml.cs`: WinUI 3 floating card interface, Mica backdrop, fluid dragging, and list management.
- `Native/ShellExtension/`: Native C++ COM shell extension implementing `IExplorerCommand` for Windows 11 File Explorer.
- `SparsePackage/`: `AppxManifest.xml` and visual assets providing package identity and startup task definition.
- `Services/PdfService.cs`: High-fidelity PDF generation, lossless image pass-through, and PDF merge engine.
- `Services/StartupService.cs`: Windows 11 Startup Apps registration management (`HKCU\...\Run`).
- `Services/ContextMenuService.cs`: Modern and classic Explorer context menu registration.
- `Services/SilentBatchManager.cs`: Concurrent batch coordinator for Explorer multi-select invocations.
- `Services/TrayIconService.cs`: Win32 system tray notification icon and context menu.
- `Services/ToastNotificationService.cs`: Interactive Windows 11 App Notifications.
- `installer/`: Standalone installer application (`PDFWin-Setup.exe`).
- `tests/PDFWin.Tests/`: Unit test suite covering PDF generation, sorting, command-line parsing, and mutex resilience.

---

## Building from Source

### Prerequisites
- Windows 11 (build 22000 or higher recommended)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 / 2026 with C++ Desktop Development workload (MSVC v143/v144)
- Windows App SDK 1.5+

### Build Steps
```powershell
# 1. Compile Native C++ Shell Extension DLL
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cl /O2 /LD /EHsc Native\ShellExtension\PDFWinShellExt.cpp /link /DEF:Native\ShellExtension\PDFWinShellExt.def /MACHINE:X64 /OUT:Native\ShellExtension\PDFWinShellExt.dll

# 2. Build and run unit tests
dotnet test tests\PDFWin.Tests\PDFWin.Tests.csproj

# 3. Publish PDFWin
dotnet publish PDFWin.csproj -c Release -r win-x64 --self-contained
```

---

## Author

Developed by **Manuel Contrera**  
GitHub: [@manuelcontrera](https://github.com/manuelcontrera)
