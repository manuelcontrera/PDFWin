# PDFWin - Native Windows 11 Utility (WinUI 3)

[![Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-0078D4)](https://github.com/microsoft/WindowsAppSDK)
[![Installer Size](https://img.shields.io/badge/Installer-26.5%20MB-brightgreen)](https://github.com/manuelcontrera/PDFWin/releases)
[![Disk Footprint](https://img.shields.io/badge/Disk%20Footprint-37%20MB-brightgreen)](https://github.com/manuelcontrera/PDFWin)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**PDFWin** is a fast, ultra-lightweight Windows 11 utility built with **WinUI 3** and **.NET 8**. It lets you convert and merge images (JPG, PNG, WebP, TIFF, BMP) and PDF documents into a single optimized PDF with zero quality loss.

It integrates directly into the **Windows 11 primary context menu** as **"Create PDF"** (no *"Show more options"* needed) and offers an optional **always-on-top floating card** accessible via **`Win + Alt + P`**.

---

## Screenshots

<div align="center">

### 1. Ready & Waiting (Drop items here)
A clean, unobtrusive floating card that stays on top while you collect files.

![PDFWin - Empty State](docs/screenshots/pdfwin-empty-card.png)

<br/>

### 2. Smart Drag & Drop Queue
Instant reordering, natural numerical sorting (`img_1`, `img_2`, `img_10`), and quick actions.

![PDFWin - Populated Queue](docs/screenshots/pdfwin-populated-queue.png)

<br/>

### 3. High-Fidelity Combined PDF
Converts or merges files silently in the background, outputting directly to your destination folder.

![PDFWin - Combined Document Output](docs/screenshots/pdfwin-combined-pdf.png)

</div>

---

## Highlights

- ⚡ **Native Windows 11 Context Menu**: Right-click any image, PDF, selection, or folder to find **"Create PDF"** on the main menu.
- 🪟 **Floating Fluent Card**: Toggle instantly with **`Win + Alt + P`** or via the system tray icon next to the clock.
- 🎯 **Lossless Engine**: JPEG stream pass-through (zero generation loss), lossless Flate PNG, and WIC hardware-accelerated decoding.
- 🪶 **Ultra-Lightweight & Compact**: Standalone installer under **27 MB**, only **~37 MB** on disk (via transparent LZX compression), and 0% CPU at idle.
- 🚀 **Official Startup App**: Clean registration in Windows 11 `Settings > Apps > Startup` (launches minimized to tray with `--startup`).

---

## Installation

1. Download **`PDFWin-Setup.exe`** from **[Releases](https://github.com/manuelcontrera/PDFWin/releases)**.
2. Run the installer (installs to `%LOCALAPPDATA%\Programs\PDFWin` without requiring Administrator privileges).
3. To uninstall, use **Windows Settings > Installed Apps** or run `Uninstall.exe`.

---

## How to Use

- **Context Menu**: Right-click files/folders in File Explorer ➔ Select **Create PDF**. A toast notification will pop up when done.
- **Floating Window**: Press **`Win + Alt + P`** ➔ Drag and drop images/PDFs ➔ Reorder if desired ➔ Click **Create PDF**.

---

## CLI Options

| Option | Description |
| :--- | :--- |
| `--silent-create <files...>` | Silently merges the specified files and shows a completion toast. |
| `--folder <folder_path>` | Merges all supported images and PDFs inside the folder. |
| `--startup` / `--minimized` | Starts minimized in the system tray. |
| `--register-menu` / `--unregister-menu` | Registers or unregisters Explorer context menu entries. |

---

## License & Author

Developed by **[Manuel Contrera](https://github.com/manuelcontrera)** under the [MIT License](LICENSE).
