using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PDFWin.Services;

public static class ContextMenuService
{
    private const string MenuKeyName = "PDFWin.CreatePdf";
    private const string MenuTitle = "Crear PDF con PDFWin";

    private static readonly string[] SupportedAssociations =
    {
        "image", ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp"
    };

    public static bool IsRegistryRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\image\shell\" + MenuKeyName);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers context menu for Windows 11 (Modern context menu via Sparse Package)
    /// and fallback registry associations for classic context menu.
    /// </summary>
    public static bool RegisterContextMenu(string rawExePath)
    {
        try
        {
            string exePath = rawExePath.Replace("\"", "").Trim();
            if (string.IsNullOrEmpty(exePath)) return false;

            string appDir = Path.GetDirectoryName(exePath) ?? "";
            string manifestPath = Path.Combine(appDir, "AppxManifest.xml");

            // 1. If AppxManifest exists, register Modern Windows 11 Package
            if (File.Exists(manifestPath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Add-AppxPackage -Register '{manifestPath}' -ExternalLocation '{appDir}' -ForceApplicationShutdown\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(5000);
                }
                catch (Exception pEx)
                {
                    Debug.WriteLine($"Error registering modern package: {pEx.Message}");
                }
            }

            // 2. Register classic fallback associations in HKCU
            string command = $"\"{exePath}\" --silent-create \"%1\"";

            foreach (var assoc in SupportedAssociations)
            {
                string title = assoc.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "Unir con PDFWin"
                    : MenuTitle;

                RegisterAssociation($@"Software\Classes\SystemFileAssociations\{assoc}\shell", MenuKeyName, title, command, exePath);
            }

            // 3. For directories
            string dirCommand = $"\"{exePath}\" --folder \"%1\"";
            RegisterAssociation(@"Software\Classes\Directory\shell", MenuKeyName, MenuTitle, dirCommand, exePath);
            RegisterAssociation(@"Software\Classes\Directory\Background\shell", MenuKeyName, "Crear PDF con imágenes de esta carpeta", $"\"{exePath}\" --folder \"%V\"", exePath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering context menu: {ex.Message}");
            return false;
        }
    }

    public static bool UnregisterContextMenu()
    {
        try
        {
            // 1. Unregister Modern Windows 11 Package
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
            catch (Exception pEx)
            {
                Debug.WriteLine($"Error unregistering modern package: {pEx.Message}");
            }

            // 2. Unregister classic associations
            foreach (var assoc in SupportedAssociations)
            {
                UnregisterAssociation($@"Software\Classes\SystemFileAssociations\{assoc}\shell", MenuKeyName);
            }

            UnregisterAssociation(@"Software\Classes\Directory\shell", MenuKeyName);
            UnregisterAssociation(@"Software\Classes\Directory\Background\shell", MenuKeyName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error unregistering context menu: {ex.Message}");
            return false;
        }
    }

    private static void RegisterAssociation(string parentKeyPath, string keyName, string title, string command, string iconPath)
    {
        using var parentKey = Registry.CurrentUser.CreateSubKey(parentKeyPath);
        if (parentKey == null) return;

        using var menuKey = parentKey.CreateSubKey(keyName);
        if (menuKey == null) return;

        menuKey.SetValue("", title);
        menuKey.SetValue("Icon", iconPath);

        using var cmdKey = menuKey.CreateSubKey("command");
        cmdKey?.SetValue("", command);
    }

    private static void UnregisterAssociation(string parentKeyPath, string keyName)
    {
        using var parentKey = Registry.CurrentUser.OpenSubKey(parentKeyPath, true);
        parentKey?.DeleteSubKeyTree(keyName, false);
    }
}
