using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PDFWin.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PDFWin.Tests;

public class PdfServiceTests : IDisposable
{
    private readonly string _testDir;

    public PdfServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PDFWin_Tests_" + Guid.NewGuid().ToString("N"));
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

    [Theory]
    [InlineData("test.jpg", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("document.pdf", true)]
    [InlineData("image.webp", true)]
    [InlineData("scan.TIFF", true)]
    [InlineData("graphic.bmp", true)]
    [InlineData("anim.gif", true)]
    [InlineData("file.txt", false)]
    [InlineData("app.exe", false)]
    [InlineData("doc.docx", false)]
    public void IsSupported_IdentifiesSupportedFilesCorrectly(string fileName, bool expected)
    {
        Assert.Equal(expected, PdfService.IsSupported(fileName));
    }

    [Fact]
    public void IsPdf_And_IsImage_ClassifyCorrectly()
    {
        Assert.True(PdfService.IsPdf("document.pdf"));
        Assert.False(PdfService.IsPdf("image.png"));

        Assert.True(PdfService.IsImage("image.png"));
        Assert.False(PdfService.IsImage("document.pdf"));
    }

    [Fact]
    public void SortNaturally_SortsPathsByFileNameNumerically()
    {
        var paths = new List<string>
        {
            Path.Combine(_testDir, "page10.jpg"),
            Path.Combine(_testDir, "page1.jpg"),
            Path.Combine(_testDir, "page2.jpg")
        };

        var sorted = PdfService.SortNaturally(paths);

        Assert.Equal(Path.Combine(_testDir, "page1.jpg"), sorted[0]);
        Assert.Equal(Path.Combine(_testDir, "page2.jpg"), sorted[1]);
        Assert.Equal(Path.Combine(_testDir, "page10.jpg"), sorted[2]);
    }

    [Fact]
    public async Task CreatePdfAsync_ThrowsOnEmptyFileList()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PdfService.CreatePdfAsync(Array.Empty<string>(), _testDir));
    }

    [Fact]
    public async Task CreatePdfAsync_FromSingleBmp_GeneratesValidPdf()
    {
        string bmpPath = CreateSampleBmp("sample.bmp", 100, 100);

        string outputPdf = await PdfService.CreatePdfAsync(new[] { bmpPath }, _testDir, "output_single.pdf");

        Assert.True(File.Exists(outputPdf));
        Assert.True(new FileInfo(outputPdf).Length > 0);

        using var doc = PdfReader.Open(outputPdf, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public async Task CreatePdfAsync_FromMultipleImages_GeneratesMultiPagePdf()
    {
        string img1 = CreateSampleBmp("img1.bmp", 50, 50);
        string img2 = CreateSampleBmp("img2.bmp", 60, 60);

        string outputPdf = await PdfService.CreatePdfAsync(new[] { img1, img2 }, _testDir, "combined.pdf");

        Assert.True(File.Exists(outputPdf));
        using var doc = PdfReader.Open(outputPdf, PdfDocumentOpenMode.Import);
        Assert.Equal(2, doc.PageCount);
    }

    [Fact]
    public async Task CreatePdfAsync_DisambiguatesExistingFileNames()
    {
        string img1 = CreateSampleBmp("img_dup.bmp", 50, 50);
        string initialPath = Path.Combine(_testDir, "dup.pdf");
        File.WriteAllText(initialPath, "placeholder");

        string result = await PdfService.CreatePdfAsync(new[] { img1 }, _testDir, "dup.pdf");

        Assert.True(File.Exists(result));
        Assert.EndsWith("dup (1).pdf", result);
    }

    [Fact]
    public async Task CreatePdfAsync_MergesExistingPdfs_PreservesAllPages()
    {
        string pdf1 = CreateSamplePdf("doc1.pdf", 2);
        string pdf2 = CreateSamplePdf("doc2.pdf", 3);

        string outputPdf = await PdfService.CreatePdfAsync(new[] { pdf1, pdf2 }, _testDir, "merged.pdf");

        Assert.True(File.Exists(outputPdf));
        using var doc = PdfReader.Open(outputPdf, PdfDocumentOpenMode.Import);
        Assert.Equal(5, doc.PageCount);
    }

    [Fact]
    public async Task CreatePdfAsync_MergesPdfAndImage_CombinedCorrectly()
    {
        string pdf = CreateSamplePdf("doc.pdf", 2);
        string img = CreateSampleBmp("photo.bmp", 80, 80);

        string outputPdf = await PdfService.CreatePdfAsync(new[] { pdf, img }, _testDir, "merged_mix.pdf");

        Assert.True(File.Exists(outputPdf));
        using var doc = PdfReader.Open(outputPdf, PdfDocumentOpenMode.Import);
        Assert.Equal(3, doc.PageCount);
    }

    private string CreateSamplePdf(string fileName, int pageCount)
    {
        string path = Path.Combine(_testDir, fileName);
        using var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            doc.AddPage();
        }
        doc.Save(path);
        return path;
    }

    private string CreateSampleBmp(string fileName, int width, int height)
    {
        string path = Path.Combine(_testDir, fileName);
        int rowStride = ((width * 3 + 3) / 4) * 4;
        int imageSize = rowStride * height;
        int fileSize = 54 + imageSize;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // BMP Header
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0); // reserved
        writer.Write(54); // pixel data offset

        // DIB Header (BITMAPINFOHEADER)
        writer.Write(40); // header size
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1); // color planes
        writer.Write((short)24); // 24 bpp
        writer.Write(0); // compression BI_RGB
        writer.Write(imageSize);
        writer.Write(2835); // horizontal resolution ~72 DPI
        writer.Write(2835); // vertical resolution ~72 DPI
        writer.Write(0); // colors in palette
        writer.Write(0); // important colors

        // Pixel data (simple blue/red pattern)
        byte[] row = new byte[rowStride];
        for (int x = 0; x < width; x++)
        {
            row[x * 3] = 200;     // B
            row[x * 3 + 1] = 100; // G
            row[x * 3 + 2] = 50;  // R
        }

        for (int y = 0; y < height; y++)
        {
            writer.Write(row);
        }

        return path;
    }
}
