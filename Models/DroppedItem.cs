using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace PDFWin.Models;

public class DroppedItem : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    private int _displayIndex;

    public string FilePath { get; }
    public string FileName { get; }
    public string Extension { get; }
    public long FileSizeBytes { get; }
    public string FileSizeFormatted { get; }
    public bool IsPdf { get; }

    public string TypeIconGlyph => IsPdf ? "\uEA90" : "\uEB9F";
    public Visibility ThumbnailVisibility => _thumbnail != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IconFallbackVisibility => _thumbnail == null ? Visibility.Visible : Visibility.Collapsed;

    public int DisplayIndex
    {
        get => _displayIndex;
        set
        {
            if (_displayIndex != value)
            {
                _displayIndex = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThumbnailVisibility));
            OnPropertyChanged(nameof(IconFallbackVisibility));
        }
    }

    public DroppedItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Extension = Path.GetExtension(filePath).ToUpperInvariant();
        IsPdf = Extension.Equals(".PDF", StringComparison.OrdinalIgnoreCase);

        try
        {
            var info = new FileInfo(filePath);
            FileSizeBytes = info.Length;
            FileSizeFormatted = FormatBytes(FileSizeBytes);
        }
        catch
        {
            FileSizeBytes = 0;
            FileSizeFormatted = "0 KB";
        }
    }

    public async Task LoadThumbnailAsync()
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(FilePath);
            using var thumb = await storageFile.GetThumbnailAsync(
                ThumbnailMode.PicturesView,
                160,
                ThumbnailOptions.UseCurrentScale);

            if (thumb != null)
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumb);
                Thumbnail = bitmap;
            }
        }
        catch
        {
            // Fallback to icon
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
