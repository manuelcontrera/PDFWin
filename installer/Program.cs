using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace PDFWin.Installer;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const uint MB_ICONERROR = 0x00000010;

    [STAThread]
    public static void Main(string[] args)
    {
        bool silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase) || a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        bool isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));

        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PDFWin");

        if (isUninstall)
        {
            PerformUninstall(targetDir, silent);
            return;
        }

        PerformInstall(targetDir, silent);
    }

    private static void PerformInstall(string targetDir, bool silent)
    {
        try
        {
            // 1. Close any running PDFWin process
            foreach (var proc in Process.GetProcessesByName("PDFWin"))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch { }
            }

            // 2. Prepare target directory
            Directory.CreateDirectory(targetDir);

            // 3. Extract embedded payload
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("PDFWin.Installer.app.zip"))
            {
                if (stream == null)
                {
                    ShowMessage("Error crítico: No se encontró el recurso de instalación embebido.", "Error de Instalación", true, silent);
                    return;
                }

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                        continue;
                    }

                    string destPath = Path.Combine(targetDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                    catch (IOException)
                    {
                        // Retry once after brief pause if file in use
                        Thread.Sleep(500);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }
            }

            // Copy self as Uninstall.exe
            try
            {
                string selfPath = Environment.ProcessPath ?? "";
                if (File.Exists(selfPath))
                {
                    File.Copy(selfPath, Path.Combine(targetDir, "Uninstall.exe"), true);
                }
            }
            catch { }

            string targetExe = Path.Combine(targetDir, "PDFWin.exe");

            // 4. Create Desktop & Start Menu Shortcuts via WScript.Shell
            CreateShortcuts(targetExe, targetDir);

            // 5. Register Modern Windows 11 Context Menu
            string manifestPath = Path.Combine(targetDir, "AppxManifest.xml");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Add-AppxPackage -Register '{manifestPath}' -ExternalLocation '{targetDir}' -ForceApplicationShutdown\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(6000);
                }
                catch { }
            }

            // 6. Register Classic Context Menu Fallback
            if (File.Exists(targetExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = targetExe,
                        Arguments = "--register-menu",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(4000);
                }
                catch { }
            }

            // 7. Register in Windows Add/Remove Programs (Installed Apps)
            RegisterInControlPanel(targetDir, targetExe);

            // 8. Launch PDFWin
            if (File.Exists(targetExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }

            ShowMessage(
                "¡PDFWin se ha instalado con éxito!\n\n" +
                "• Icono en la barra de tareas / bandeja del sistema junto al reloj.\n" +
                "• Atajo global: Win + Alt + P para abrir u ocultar la ventana.\n" +
                "• Menú contextual: Haz clic derecho sobre cualquier imagen, PDF o carpeta y verás 'Crear PDF con PDFWin' directamente en el menú principal de Windows 11.",
                "PDFWin - Instalación Completada",
                false,
                silent);
        }
        catch (Exception ex)
        {
            ShowMessage($"Ocurrió un error al instalar PDFWin:\n\n{ex.Message}", "Error de Instalación", true, silent);
        }
    }

    private static void PerformUninstall(string targetDir, bool silent)
    {
        try
        {
            // Close running PDFWin
            foreach (var proc in Process.GetProcessesByName("PDFWin"))
            {
                try { proc.Kill(); proc.WaitForExit(3000); } catch { }
            }

            // 1. Unregister Windows 11 Modern Context Menu
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -WindowStyle Hidden -Command \"Get-AppxPackage -Name '*PDFWin*' | Remove-AppxPackage\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { }

            // 2. Unregister Classic Registry keys
            try
            {
                string targetExe = Path.Combine(targetDir, "PDFWin.exe");
                if (File.Exists(targetExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = targetExe,
                        Arguments = "--unregister-menu",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                }
            }
            catch { }

            // 3. Remove shortcuts
            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PDFWin.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                string startLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PDFWin.lnk");
                if (File.Exists(startLnk)) File.Delete(startLnk);
            }
            catch { }

            // 4. Remove registry entry from Installed Apps
            try
            {
                using var uninstKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
                uninstKey?.DeleteSubKeyTree("PDFWin", false);
            }
            catch { }

            // 5. Schedule folder deletion on reboot/exit if needed
            ShowMessage("PDFWin ha sido desinstalado correctamente de tu equipo.", "PDFWin - Desinstalación", false, silent);
        }
        catch (Exception ex)
        {
            ShowMessage($"Aviso durante la desinstalación: {ex.Message}", "PDFWin", true, silent);
        }
    }

    private static void CreateShortcuts(string targetExe, string targetDir)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            // Desktop shortcut
            dynamic desktopShortcut = shell.CreateShortcut(Path.Combine(desktopPath, "PDFWin.lnk"));
            desktopShortcut.TargetPath = targetExe;
            desktopShortcut.WorkingDirectory = targetDir;
            desktopShortcut.IconLocation = $"{targetExe},0";
            desktopShortcut.Description = "PDFWin - Creación y unión de PDFs para Windows 11";
            desktopShortcut.Save();

            // Start Menu shortcut
            dynamic startShortcut = shell.CreateShortcut(Path.Combine(startMenuPath, "PDFWin.lnk"));
            startShortcut.TargetPath = targetExe;
            startShortcut.WorkingDirectory = targetDir;
            startShortcut.IconLocation = $"{targetExe},0";
            startShortcut.Description = "PDFWin - Creación y unión de PDFs para Windows 11";
            startShortcut.Save();
        }
        catch { }
    }

    private static void RegisterInControlPanel(string targetDir, string targetExe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PDFWin");
            if (key == null) return;

            key.SetValue("DisplayName", "PDFWin");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "Manuel Contrera");
            key.SetValue("DisplayIcon", $"{targetExe},0");
            key.SetValue("InstallLocation", targetDir);
            key.SetValue("UninstallString", $"\"{Path.Combine(targetDir, "Uninstall.exe")}\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch { }
    }

    private static void ShowMessage(string text, string title, bool isError, bool silent)
    {
        if (silent) return;
        uint icon = isError ? MB_ICONERROR : MB_ICONINFORMATION;
        MessageBoxW(IntPtr.Zero, text, title, MB_OK | icon);
    }
}
