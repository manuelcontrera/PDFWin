using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PDFWin.Models;
using PDFWin.Native;
using PDFWin.Services;
using WinRT.Interop;

namespace PDFWin;

public sealed partial class MainWindow : Window
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private OverlappedPresenter? _presenter;
    private bool _isExpanded = false;
    private const int CompactWidth = 260;
    private const int CompactHeight = 260;
    private const int ExpandedWidth = 360;
    private const int ExpandedHeight = 480;

    private TrayIconService? _trayIconService;
    private HotkeyService? _hotkeyService;
    private Win32Helper.WndProcDelegate? _wndProcDelegate;
    private IntPtr _originalWndProc = IntPtr.Zero;

    public ObservableCollection<DroppedItem> DroppedItems { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        SetupWindowStyle();
        RestoreWindowPosition();
        HookWndProc();

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        _trayIconService = new TrayIconService(_hwnd, ToggleVisibility, ExitApp);
        _hotkeyService = new HotkeyService(_hwnd);
        ToastNotificationService.Initialize();

        _appWindow.Changed += AppWindow_Changed;
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (s, e) => ApplyWindowBorderColor();
        }
    }

    private void SetupWindowStyle()
    {
        // Customize window presenter for borderless floating behavior
        _presenter = _appWindow.Presenter as OverlappedPresenter;
        if (_presenter != null)
        {
            _presenter.IsAlwaysOnTop = SettingsService.Current.AlwaysOnTop;
            _presenter.IsResizable = false;
            _presenter.IsMaximizable = false;
            _presenter.IsMinimizable = false;
            _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }

        // 2. Strip any remaining window borders and non-client frames at Win32 level
        int style = Win32Helper.GetWindowLong(_hwnd, Win32Helper.GWL_STYLE);
        style &= ~Win32Helper.WS_BORDER;
        style &= ~Win32Helper.WS_THICKFRAME;
        style &= ~Win32Helper.WS_CAPTION;
        Win32Helper.SetWindowLong(_hwnd, Win32Helper.GWL_STYLE, style);

        int exStyle = Win32Helper.GetWindowLong(_hwnd, Win32Helper.GWL_EXSTYLE);
        exStyle &= ~Win32Helper.WS_EX_WINDOWEDGE;
        exStyle &= ~Win32Helper.WS_EX_CLIENTEDGE;
        exStyle &= ~Win32Helper.WS_EX_STATICEDGE;
        exStyle &= ~Win32Helper.WS_EX_DLGMODALFRAME;
        Win32Helper.SetWindowLong(_hwnd, Win32Helper.GWL_EXSTYLE, exStyle);

        // 3. Extend DWM frame into client area to enable true seamless composition and round corners
        var margins = new Win32Helper.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        Win32Helper.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // 4. Set native Windows 11 rounded corners (DWMWCP_ROUND)
        int corner = Win32Helper.DWMWCP_ROUND;
        Win32Helper.DwmSetWindowAttribute(_hwnd, Win32Helper.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // 5. Apply subtle thin light contrast border (matching Windows 11 Fluent / Dropshelf style)
        ApplyWindowBorderColor();

        // 6. Notify the window of frame change so DWM applies changes immediately
        Win32Helper.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOZORDER | Win32Helper.SWP_FRAMECHANGED);

        // Start in compact mode with DPI scaling
        ResizeWindowToDip(CompactWidth, CompactHeight);
    }

    private void ApplyWindowBorderColor()
    {
        bool isDark = true;
        if (Content is FrameworkElement rootElement)
        {
            isDark = rootElement.ActualTheme != ElementTheme.Light;
        }
        else
        {
            isDark = Application.Current.RequestedTheme != ApplicationTheme.Light;
        }

        // Subtle thin contrast border matching Windows 11 Fluent / Dropshelf style:
        // Dark theme: subtle light border (RGB: 75, 75, 82 / #4B4B52 -> COLORREF 0x00524B4B)
        // Light theme: subtle dark border (RGB: 210, 210, 215 / #D2D2D7 -> COLORREF 0x00D7D2D2)
        int borderColor = isDark
            ? Win32Helper.RGB(75, 75, 82)
            : Win32Helper.RGB(210, 210, 215);

        Win32Helper.DwmSetWindowAttribute(_hwnd, Win32Helper.DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
    }

    private float GetDpiScale()
    {
        uint dpi = Win32Helper.GetDpiForWindow(_hwnd);
        return dpi > 0 ? (dpi / 96.0f) : 1.0f;
    }

    private void ResizeWindowToDip(int dipWidth, int dipHeight)
    {
        float scale = GetDpiScale();
        _appWindow.Resize(new SizeInt32((int)Math.Round(dipWidth * scale), (int)Math.Round(dipHeight * scale)));
    }

    private void HookWndProc()
    {
        _wndProcDelegate = new Win32Helper.WndProcDelegate(SubclassWndProc);
        IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _originalWndProc = Win32Helper.SetWindowLongPtr(_hwnd, Win32Helper.GWLP_WNDPROC, newWndProc);
    }

    private static readonly uint WM_SHOW_PDFWIN = Win32Helper.RegisterWindowMessage("PDFWin_Show_Command");

    private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SHOW_PDFWIN)
        {
            _appWindow.Show();
            Win32Helper.SetForegroundWindow(_hwnd);
            return IntPtr.Zero;
        }
        else if (msg == Win32Helper.WM_HOTKEY)
        {
            if (wParam.ToInt32() == HotkeyService.HOTKEY_ID)
            {
                ToggleVisibility();
                return IntPtr.Zero;
            }
        }
        else if (msg == Win32Helper.WM_TRAYICON)
        {
            _trayIconService?.HandleTrayMessage(lParam);
            return IntPtr.Zero;
        }

        return Win32Helper.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    #region Fluid Drag & Move Window

    private bool _isDragging = false;
    private Win32Helper.POINT _dragStartCursorPos;
    private PointInt32 _dragStartWindowPos;

    private void DragArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(sender as UIElement).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            Win32Helper.GetCursorPos(out _dragStartCursorPos);
            _dragStartWindowPos = _appWindow.Position;
            if (sender is UIElement el)
            {
                el.CapturePointer(e.Pointer);
            }
            e.Handled = true;
        }
    }

    private void DragArea_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            Win32Helper.GetCursorPos(out var currentCursor);
            int deltaX = currentCursor.X - _dragStartCursorPos.X;
            int deltaY = currentCursor.Y - _dragStartCursorPos.Y;
            _appWindow.Move(new PointInt32(_dragStartWindowPos.X + deltaX, _dragStartWindowPos.Y + deltaY));
            e.Handled = true;
        }
    }

    private void DragArea_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            if (sender is UIElement el)
            {
                el.ReleasePointerCapture(e.Pointer);
            }
            SettingsService.Save();
            e.Handled = true;
        }
    }

    private void DragArea_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        SettingsService.Save();
    }

    #endregion

    private void RestoreWindowPosition()
    {
        int x = SettingsService.Current.WindowX;
        int y = SettingsService.Current.WindowY;

        if (x >= 0 && y >= 0)
        {
            _appWindow.Move(new PointInt32(x, y));
        }
        else
        {
            // Center on primary screen initially with DPI accounted for
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            float scale = GetDpiScale();
            int physicalWidth = (int)Math.Round(CompactWidth * scale);
            int physicalHeight = (int)Math.Round(CompactHeight * scale);
            int centerX = (displayArea.WorkArea.Width - physicalWidth) / 2;
            int centerY = (displayArea.WorkArea.Height - physicalHeight) / 2;
            _appWindow.Move(new PointInt32(centerX, centerY));
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange)
        {
            var pos = sender.Position;
            SettingsService.Current.WindowX = pos.X;
            SettingsService.Current.WindowY = pos.Y;
            // Saved to disk on drag release or app exit to prevent excessive I/O
        }
    }

    public void ToggleVisibility()
    {
        if (_appWindow.IsVisible)
        {
            SettingsService.Save();
            _appWindow.Hide();
        }
        else
        {
            _appWindow.Show();
            Win32Helper.SetForegroundWindow(_hwnd);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Save();
        _appWindow.Hide();
    }

    public void ExitApp()
    {
        _trayIconService?.Dispose();
        _hotkeyService?.Dispose();
        SettingsService.Save();
        Application.Current.Exit();
    }

    #region Drag and Drop

    private void RootContainer_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Soltar para añadir a PDFWin";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void RootContainer_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        var validFiles = new List<string>();

        foreach (var item in items)
        {
            if (item is StorageFile file && PdfService.IsSupported(file.Path))
            {
                validFiles.Add(file.Path);
            }
            else if (item is StorageFolder folder)
            {
                var folderFiles = Directory.GetFiles(folder.Path)
                    .Where(PdfService.IsSupported);
                validFiles.AddRange(folderFiles);
            }
        }

        if (validFiles.Count == 0) return;

        // Apply natural alphanumeric sort
        var sorted = PdfService.SortNaturally(validFiles);

        foreach (var path in sorted)
        {
            if (!DroppedItems.Any(x => x.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                var dropped = new DroppedItem(path);
                DroppedItems.Add(dropped);
                _ = dropped.LoadThumbnailAsync();
            }
        }

        UpdateUiState();
    }

    #endregion

    private void UpdateUiState()
    {
        for (int i = 0; i < DroppedItems.Count; i++)
        {
            DroppedItems[i].DisplayIndex = i + 1;
        }

        int count = DroppedItems.Count;
        TxtItemCount.Text = $"{count} {(count == 1 ? "item" : "items")}";

        if (count > 0 && !_isExpanded)
        {
            _isExpanded = true;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            PopulatedStatePanel.Visibility = Visibility.Visible;
            ResizeWindowToDip(ExpandedWidth, ExpandedHeight);
        }
        else if (count == 0 && _isExpanded)
        {
            _isExpanded = false;
            EmptyStatePanel.Visibility = Visibility.Visible;
            PopulatedStatePanel.Visibility = Visibility.Collapsed;
            ResizeWindowToDip(CompactWidth, CompactHeight);
        }
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DroppedItem item)
        {
            DroppedItems.Remove(item);
            UpdateUiState();
        }
    }

    private async void BtnCreatePdf_Click(object sender, RoutedEventArgs e)
    {
        if (DroppedItems.Count == 0) return;

        ProcessingOverlay.Visibility = Visibility.Visible;
        TxtProcessingStatus.Text = "Generando PDF...";

        try
        {
            var files = DroppedItems.Select(x => x.FilePath).ToList();
            string outputPath = await PdfService.CreatePdfAsync(files);

            ToastNotificationService.ShowPdfCreated(outputPath);

            DroppedItems.Clear();
            UpdateUiState();
        }
        catch (Exception ex)
        {
            TxtProcessingStatus.Text = $"Error: {ex.Message}";
            await Task.Delay(2500);
        }
        finally
        {
            ProcessingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnItemsBadge_Click(object sender, RoutedEventArgs e)
    {
        if (DroppedItems.Count == 0) return;

        var menu = new MenuFlyout();

        var sortItem = new MenuFlyoutItem { Text = "Ordenar alfabéticamente" };
        sortItem.Click += (s, ev) =>
        {
            var sorted = DroppedItems.OrderBy(x => x.FileName, NaturalStringComparer.Instance).ToList();
            DroppedItems.Clear();
            foreach (var item in sorted) DroppedItems.Add(item);
            UpdateUiState();
        };
        menu.Items.Add(sortItem);

        var clearItem = new MenuFlyoutItem { Text = "Limpiar todo" };
        clearItem.Click += (s, ev) =>
        {
            DroppedItems.Clear();
            UpdateUiState();
        };
        menu.Items.Add(clearItem);

        menu.ShowAt(BtnItemsBadge);
    }
}
