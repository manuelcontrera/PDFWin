using System;
using System.IO;
using System.Text.Json;

namespace PDFWin.Services;

public class AppSettings
{
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public bool AlwaysOnTop { get; set; } = true;
    public string Hotkey { get; set; } = "Win+Alt+P";
}

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PDFWin");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

    private static AppSettings? _currentSettings;

    public static AppSettings Current
    {
        get
        {
            if (_currentSettings == null)
            {
                Load();
            }
            return _currentSettings!;
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                _currentSettings = JsonSerializer.Deserialize<AppSettings>(json);
            }
        }
        catch
        {
            // Ignore corrupted settings
        }

        _currentSettings ??= new AppSettings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore write failures in read-only setups
        }
    }
}
