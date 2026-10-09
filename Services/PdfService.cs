using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using PDFWin.Native;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFWin.Services;

public static class PdfService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp", ".gif"
    };

    public static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) || SupportedImageExtensions.Contains(ext);
    }

    public static bool IsImage(string path)
    {
        string ext = Path.GetExtension(path);
        return SupportedImageExtensions.Contains(ext);
    }

    public static bool IsPdf(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Natural alphanumeric sort matching Windows Explorer logic
    /// </summary>
    public static List<string> SortNaturally(IEnumerable<string> paths)
    {
        return paths.OrderBy(p => Path.GetFileName(p), NaturalStringComparer.Instance).ToList();
    }

    /// <summary>
    /// Converts and merges images and PDFs into a single optimized PDF document.
    /// Preserves original image DCT streams (lossless) and clones PDF page trees.
    /// </summary>
    public static async Task<string> CreatePdfAsync(
        IReadOnlyList<string> filePaths,
        string? outputDirectory = null,
        string? customFileName = null,
        IProgress<double>? progress = null)
    {
        if (filePaths == null || filePaths.Count == 0)
        {
            throw new ArgumentException("No hay archivos para convertir.", nameof(filePaths));
        }

        // Determine destination folder with read-only fallback probe
        string targetDir = outputDirectory ?? Path.GetDirectoryName(filePaths[0]) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        try
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            string probeFile = Path.Combine(targetDir, $".pdfwin_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "test");
            File.Delete(probeFile);
        }
        catch
        {
            targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
        }

        // Determine destination filename
        string fileName;
        if (!string.IsNullOrWhiteSpace(customFileName))
        {
            fileName = customFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? customFileName : customFileName + ".pdf";
        }
        else if (filePaths.Count == 1)
        {
            string baseName = Path.GetFileNameWithoutExtension(filePaths[0]);
            fileName = $"{baseName}.pdf";
        }
        else
        {
            fileName = "Documento_Combinado.pdf";
        }

        string outputPath = GetUniqueFilePath(targetDir, fileName);

        using var outputDoc = new PdfDocument();
        outputDoc.Info.Creator = "PDFWin - Windows 11 Native";
        outputDoc.Info.CreationDate = DateTime.Now;

        int totalCount = filePaths.Count;

        for (int i = 0; i < totalCount; i++)
        {
            string filePath = filePaths[i];
            if (!File.Exists(filePath)) continue;

            string ext = Path.GetExtension(filePath);

            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Import existing PDF pages directly (vector, fonts and streams stay 100% intact)
                    using var inputPdf = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                    for (int pageIdx = 0; pageIdx < inputPdf.PageCount; pageIdx++)
                    {
                        outputDoc.AddPage(inputPdf.Pages[pageIdx]);
                    }
                }
                catch (PdfReaderException)
                {
                    // Password-protected or encrypted PDF - skipped safely
                }
            }
            else if (SupportedImageExtensions.Contains(ext))
            {
                // Process image
                await AddImageToDocumentAsync(outputDoc, filePath);
            }

            progress?.Report((double)(i + 1) / totalCount);
        }

        if (outputDoc.PageCount == 0)
        {
            throw new InvalidOperationException("No se pudo agregar ninguna página al PDF.");
        }

        outputDoc.Save(outputPath);
        return outputPath;
    }

    private static async Task AddImageToDocumentAsync(PdfDocument outputDoc, string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        XImage? xImage = null;
        MemoryStream? convertedStream = null;

        try
        {
            // For standard JPG and PNG, try direct loading which preserves DCT/Flate streams
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                try
                {
                    xImage = XImage.FromFile(filePath);
                }
                catch
                {
                    // Fall back to WIC decoder if PDFsharp throws on uncommon color spaces (e.g. CMYK)
                }
            }

            // If not loaded yet (e.g. WebP, TIFF, BMP, or special JPG/PNG), use Windows Imaging Component (WIC)
            if (xImage == null)
            {
                convertedStream = await DecodeImageViaWicAsync(filePath);
                if (convertedStream != null)
                {
                    xImage = XImage.FromStream(convertedStream);
                }
            }

            if (xImage == null)
            {
                throw new NotSupportedException($"No se pudo decodificar la imagen: {filePath}");
            }

            // Create page matching image aspect ratio and physical points
            var page = outputDoc.AddPage();
            page.Width = XUnit.FromPoint(xImage.PointWidth);
            page.Height = XUnit.FromPoint(xImage.PointHeight);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
        }
        finally
        {
            xImage?.Dispose();
            convertedStream?.Dispose();
        }
    }

    /// <summary>
    /// Uses native Windows 11 Windows Imaging Component (WIC) to decode WebP, TIFF, BMP, HEIC, etc.
    /// and transcode to high-quality lossless PNG stream for PDFsharp embedding.
    /// </summary>
    private static async Task<MemoryStream> DecodeImageViaWicAsync(string filePath)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.PixelWidth > 16384 || decoder.PixelHeight > 16384)
        {
            throw new NotSupportedException($"La imagen supera las dimensiones máximas permitidas ({decoder.PixelWidth}x{decoder.PixelHeight}).");
        }
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var outputMemStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputMemStream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        var dotNetStream = new MemoryStream();
        outputMemStream.Seek(0);
        await outputMemStream.AsStreamForRead().CopyToAsync(dotNetStream);
        dotNetStream.Position = 0;
        return dotNetStream;
    }

    private static string GetUniqueFilePath(string directory, string desiredName)
    {
        string fullPath = Path.Combine(directory, desiredName);
        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        string baseName = Path.GetFileNameWithoutExtension(desiredName);
        string extension = Path.GetExtension(desiredName);

        int counter = 1;
        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(directory, $"{baseName} ({counter}){extension}");
            counter++;
        }

        return fullPath;
    }
}
