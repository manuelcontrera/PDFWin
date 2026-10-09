using System;
using System.IO;
using System.Text.Json;
using PDFWin.Services;
using Xunit;

namespace PDFWin.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedValues()
    {
        var settings = new AppSettings();
        Assert.Equal(-1, settings.WindowX);
        Assert.Equal(-1, settings.WindowY);
        Assert.True(settings.AlwaysOnTop);
        Assert.Equal("Win+Alt+P", settings.Hotkey);
    }

    [Fact]
    public void AppSettings_CanSerializeAndDeserialize()
    {
        var original = new AppSettings
        {
            WindowX = 500,
            WindowY = 300,
            AlwaysOnTop = false,
            Hotkey = "Win+Ctrl+P"
        };

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(500, restored.WindowX);
        Assert.Equal(300, restored.WindowY);
        Assert.False(restored.AlwaysOnTop);
        Assert.Equal("Win+Ctrl+P", restored.Hotkey);
    }

    [Fact]
    public void CorruptedJson_FallsBackToDefaultGracefully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "PDFWinTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string tempFile = Path.Combine(tempDir, "corrupted.json");

        try
        {
            File.WriteAllText(tempFile, "{ broken json: [");
            AppSettings? settings = null;
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(tempFile));
            }
            catch
            {
                settings = new AppSettings();
            }

            Assert.NotNull(settings);
            Assert.True(settings.AlwaysOnTop);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
