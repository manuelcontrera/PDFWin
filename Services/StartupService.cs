using System;
using System.IO;
using Microsoft.Win32;

namespace PDFWin.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PDFWin";

    /// <summary>
    /// Checks whether PDFWin is configured to launch on Windows startup.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers PDFWin in the Windows Startup registry (Settings > Apps > Startup).
    /// </summary>
    public static bool RegisterStartup(string? customExePath = null)
    {
        try
        {
            string exePath = customExePath ?? Environment.ProcessPath ?? "";
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                exePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "PDFWin", "PDFWin.exe");
            }

            if (!File.Exists(exePath)) return false;

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key != null)
            {
                key.SetValue(AppName, $"\"{exePath}\" --startup");
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    /// <summary>
    /// Unregisters PDFWin from Windows Startup registry.
    /// </summary>
    public static bool UnregisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
