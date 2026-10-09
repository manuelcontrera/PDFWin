using System;
using System.IO;
using System.Threading.Tasks;
using PDFWin.Services;
using Xunit;

namespace PDFWin.Tests;

public class PdfServiceEdgeCaseTests : IDisposable
{
    private readonly string _testDir;

    public PdfServiceEdgeCaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PDFWin_EdgeCases_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CreatePdfAsync_CorruptedImage_ThrowsOrHandlesGracefully()
    {
        // A corrupted file with .jpg extension
        string fakeJpg = Path.Combine(_testDir, "corrupted.jpg");
        File.WriteAllText(fakeJpg, "This is not a real JPEG image binary data.");

        // We expect an exception or failure when decoding invalid image bytes
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await PdfService.CreatePdfAsync(new[] { fakeJpg }, _testDir, "out.pdf");
        });
    }

    [Fact]
    public async Task CreatePdfAsync_ZeroByteImage_ThrowsOrHandlesGracefully()
    {
        string zeroBytePng = Path.Combine(_testDir, "empty.png");
        File.WriteAllBytes(zeroBytePng, Array.Empty<byte>());

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await PdfService.CreatePdfAsync(new[] { zeroBytePng }, _testDir, "out_zero.pdf");
        });
    }

    [Fact]
    public async Task CreatePdfAsync_NonExistentFilesIgnored_ThrowsIfAllMissing()
    {
        string ghostFile = Path.Combine(_testDir, "ghost.png");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await PdfService.CreatePdfAsync(new[] { ghostFile }, _testDir, "out_ghost.pdf");
        });
    }
}
