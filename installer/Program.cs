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
                    ShowMessage("Critical error: Embedded installation resource was not found.", "Installation Error", true, silent);
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

            // 7. Register in Windows Startup Apps (Settings > Apps > Startup)
            try
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                runKey?.SetValue("PDFWin", $"\"{targetExe}\" --startup");
            }
            catch { }

            // 8. Register in Windows Add/Remove Programs (Installed Apps)
            RegisterInControlPanel(targetDir, targetExe);

            // 9. Launch PDFWin
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
                "PDFWin has been installed successfully!\n\n" +
                "• System tray icon next to the taskbar clock.\n" +
                "• Global shortcut: Win + Alt + P to open or hide the window.\n" +
                "• Context menu: Right-click any image, PDF or folder and select 'Create PDF' directly in the Windows 11 main menu.",
                "PDFWin - Installation Completed",
                false,
                silent);
        }
        catch (Exception ex)
        {
            ShowMessage($"An error occurred while installing PDFWin:\n\n{ex.Message}", "Installation Error", true, silent);
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

            // 4. Remove startup entry
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("PDFWin", false);
            }
            catch { }

            // 5. Remove registry entry from Installed Apps
            try
            {
                using var uninstKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
                uninstKey?.DeleteSubKeyTree("PDFWin", false);
            }
            catch { }

            // 6. Schedule folder deletion on reboot/exit if needed
            ShowMessage("PDFWin has been successfully uninstalled from your computer.", "PDFWin - Uninstall", false, silent);
        }
        catch (Exception ex)
        {
            ShowMessage($"Notice during uninstallation: {ex.Message}", "PDFWin", true, silent);
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
            desktopShortcut.Description = "PDFWin - Windows 11 PDF Creation and Merge Utility";
            desktopShortcut.Save();

            // Clean up any redundant Start Menu shortcut so only the Sparse Package app shows up in Search
            string startLnk = Path.Combine(startMenuPath, "PDFWin.lnk");
            if (File.Exists(startLnk))
            {
                try { File.Delete(startLnk); } catch { }
            }
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
            key.SetValue("DisplayVersion", "1.1.0");
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
