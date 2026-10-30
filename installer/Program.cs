using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("PDFWin Setup")]
[assembly: AssemblyDescription("PDFWin - Native Windows 11 PDF Utility Installer")]
[assembly: AssemblyCompany("Manuel Contrera")]
[assembly: AssemblyProduct("PDFWin")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

namespace PDFWin.Installer
{
    internal static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_ICONERROR = 0x00000010;

        [STAThread]
        public static void Main(string[] args)
        {
            bool silent = args.Any(a =>
                a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/S", StringComparison.OrdinalIgnoreCase));

            bool noLaunch = args.Any(a =>
                a.Equals("--no-launch", StringComparison.OrdinalIgnoreCase));

            bool isUninstall = args.Any(a =>
                a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));

            string targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "PDFWin");

            if (isUninstall)
            {
                PerformUninstall(targetDir, silent);
                return;
            }

            PerformInstall(targetDir, silent, noLaunch);
        }

        private static void PerformInstall(string targetDir, bool silent, bool noLaunch)
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

                // 3. Extract embedded payload (prefers solid LZMA2 app.tar.xz, falls back to app.zip)
                var assembly = Assembly.GetExecutingAssembly();
                using (var xzStream = assembly.GetManifestResourceStream("PDFWin.Installer.app.tar.xz"))
                {
                    if (xzStream != null)
                    {
                        ExtractTarXzPayload(xzStream, targetDir);
                    }
                    else
                    {
                        using (var zipStream = assembly.GetManifestResourceStream("PDFWin.Installer.app.zip"))
                        {
                            if (zipStream == null)
                            {
                                ShowMessage("Critical error: Embedded installation resource was not found.", "Installation Error", true, silent);
                                return;
                            }
                            ExtractZipPayload(zipStream, targetDir);
                        }
                    }
                }

                // 4. Write lightweight Uninstall.exe (or copy self as fallback)
                try
                {
                    string uninstallPath = Path.Combine(targetDir, "Uninstall.exe");
                    using (var uninstStream = assembly.GetManifestResourceStream("PDFWin.Installer.Uninstall.bin"))
                    {
                        if (uninstStream != null)
                        {
                            using (var fs = new FileStream(uninstallPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                uninstStream.CopyTo(fs);
                            }
                        }
                        else
                        {
                            string selfPath = Process.GetCurrentProcess().MainModule != null
                                ? Process.GetCurrentProcess().MainModule.FileName
                                : Assembly.GetExecutingAssembly().Location;
                            if (!string.IsNullOrEmpty(selfPath) && File.Exists(selfPath))
                            {
                                File.Copy(selfPath, uninstallPath, true);
                            }
                        }
                    }
                }
                catch { }

                string targetExe = Path.Combine(targetDir, "PDFWin.exe");

                // 4b. Apply transparent Windows LZX executable compression to minimize disk footprint
                ApplyLzxCompression(targetDir);

                // 5. Create Desktop & Start Menu Shortcuts via WScript.Shell
                CreateShortcuts(targetExe, targetDir);

                // 6. Register Modern Windows 11 Context Menu (Sparse Package)
                string manifestPath = Path.Combine(targetDir, "AppxManifest.xml");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = string.Format("-NoProfile -WindowStyle Hidden -Command \"Add-AppxPackage -Register '{0}' -ExternalLocation '{1}' -ForceApplicationShutdown\"", manifestPath, targetDir),
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (var p = Process.Start(psi))
                        {
                            if (p != null) p.WaitForExit(6000);
                        }
                    }
                    catch { }
                }

                // 7. Register Classic Context Menu Fallback
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
                        using (var p = Process.Start(psi))
                        {
                            if (p != null) p.WaitForExit(4000);
                        }
                    }
                    catch { }
                }

                // 8. Register in Windows Startup Apps (Settings > Apps > Startup)
                try
                {
                    using (var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (runKey != null)
                        {
                            runKey.SetValue("PDFWin", string.Format("\"{0}\" --startup", targetExe));
                        }
                    }
                }
                catch { }

                // 9. Register in Windows Add/Remove Programs (Installed Apps)
                RegisterInControlPanel(targetDir, targetExe);

                // 10. Launch PDFWin
                if (!noLaunch && File.Exists(targetExe))
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
                ShowMessage(string.Format("An error occurred while installing PDFWin:\n\n{0}", ex.Message), "Installation Error", true, silent);
            }
        }

        private static void ExtractTarXzPayload(Stream xzStream, string targetDir)
        {
            string tempArchive = Path.Combine(Path.GetTempPath(), string.Format("PDFWin_Setup_{0:N}.tar.xz", Guid.NewGuid()));
            try
            {
                using (var fs = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    xzStream.CopyTo(fs);
                }

                string systemTar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
                if (!File.Exists(systemTar))
                {
                    systemTar = "tar.exe";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = systemTar,
                    Arguments = string.Format("-xf \"{0}\" -C \"{1}\"", tempArchive, targetDir),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new InvalidOperationException("Failed to start system archive extractor (tar.exe).");
                    }
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        throw new IOException(string.Format("Archive extraction failed (exit code {0}): {1}", proc.ExitCode, stderr));
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempArchive)) File.Delete(tempArchive);
                }
                catch { }
            }
        }

        private static void ExtractZipPayload(Stream zipStream, string targetDir)
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                        continue;
                    }

                    string destPath = Path.Combine(targetDir, entry.FullName);
                    string destFolder = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(500);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }
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
                    using (var p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit(5000);
                    }
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
                        using (var p = Process.Start(psi))
                        {
                            if (p != null) p.WaitForExit(3000);
                        }
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
                    using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null) runKey.DeleteValue("PDFWin", false);
                    }
                }
                catch { }

                // 5. Remove registry entry from Installed Apps
                try
                {
                    using (var uninstKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        if (uninstKey != null) uninstKey.DeleteSubKeyTree("PDFWin", false);
                    }
                }
                catch { }

                // 6. Schedule folder cleanup after Uninstall.exe exits
                try
                {
                    if (Directory.Exists(targetDir))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = string.Format("/c ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"{0}\"", targetDir),
                            WorkingDirectory = Path.GetTempPath(),
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                }
                catch { }

                ShowMessage("PDFWin has been successfully uninstalled from your computer.", "PDFWin - Uninstall", false, silent);
            }
            catch (Exception ex)
            {
                ShowMessage(string.Format("Notice during uninstallation: {0}", ex.Message), "PDFWin", true, silent);
            }
        }

        private static void CreateShortcuts(string targetExe, string targetDir)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                object desktopShortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { Path.Combine(desktopPath, "PDFWin.lnk") });

                if (desktopShortcut != null)
                {
                    Type scType = desktopShortcut.GetType();
                    scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, desktopShortcut, new object[] { targetExe });
                    scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, desktopShortcut, new object[] { targetDir });
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, desktopShortcut, new object[] { string.Format("{0},0", targetExe) });
                    scType.InvokeMember("Description", BindingFlags.SetProperty, null, desktopShortcut, new object[] { "PDFWin - Windows 11 PDF Creation and Merge Utility" });
                    scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, desktopShortcut, null);
                }

                // Clean up any redundant Start Menu shortcut so only the Sparse Package app shows up in Search
                string startLnk = Path.Combine(startMenuPath, "PDFWin.lnk");
                if (File.Exists(startLnk))
                {
                    try { File.Delete(startLnk); } catch { }
                }
            }
            catch { }
        }

        private static void ApplyLzxCompression(string targetDir)
        {
            try
            {
                string compactExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "compact.exe");
                if (!File.Exists(compactExe))
                {
                    compactExe = "compact.exe";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = compactExe,
                    Arguments = string.Format("/c /s:\"{0}\" /i /exe:lzx", targetDir),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit(30000);
                    }
                }
            }
            catch { }
        }

        private static int CalculateDirectorySizeOnDiskInKB(string targetDir)
        {
            try
            {
                long totalBytes = 0;
                foreach (string file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    uint high;
                    uint low = GetCompressedFileSizeW(file, out high);
                    if (low == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0)
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                    else
                    {
                        totalBytes += ((long)high << 32) | low;
                    }
                }

                int kb = (int)(totalBytes / 1024L);
                return kb > 0 ? kb : 38000;
            }
            catch
            {
                return 38000;
            }
        }

        private static void RegisterInControlPanel(string targetDir, string targetExe)
        {
            try
            {
                int estimatedSizeKB = CalculateDirectorySizeOnDiskInKB(targetDir);
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PDFWin"))
                {
                    if (key == null) return;

                    key.SetValue("DisplayName", "PDFWin");
                    key.SetValue("DisplayVersion", "1.5.0");
                    key.SetValue("Publisher", "Manuel Contrera");
                    key.SetValue("DisplayIcon", string.Format("{0},0", targetExe));
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("UninstallString", string.Format("\"{0}\" --uninstall", Path.Combine(targetDir, "Uninstall.exe")));
                    key.SetValue("EstimatedSize", estimatedSizeKB, RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
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
}
