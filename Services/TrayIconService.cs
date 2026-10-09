using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PDFWin.Native;

namespace PDFWin.Services;

public class TrayIconService : IDisposable
{
    private const int TRAY_ID = 1001;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_DELETE = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;

    private readonly IntPtr _hwnd;
    private readonly Action _onOpenWindow;
    private readonly Action _onExit;
    private bool _isCreated = false;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    public TrayIconService(IntPtr hwnd, Action onOpenWindow, Action onExit)
    {
        _hwnd = hwnd;
        _onOpenWindow = onOpenWindow;
        _onExit = onExit;

        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
            IntPtr hIcon = IntPtr.Zero;
            if (File.Exists(iconPath))
            {
                hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            }
            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION fallback
            }

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TRAY_ID,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = Win32Helper.WM_TRAYICON,
                hIcon = hIcon,
                szTip = "PDFWin (Win+Alt+P)"
            };

            Shell_NotifyIcon(NIM_ADD, ref nid);
            _isCreated = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating tray icon: {ex.Message}");
        }
    }

    public void HandleTrayMessage(IntPtr lParam)
    {
        int msg = lParam.ToInt32();
        if (msg == Win32Helper.WM_LBUTTONUP)
        {
            _onOpenWindow();
        }
        else if (msg == Win32Helper.WM_RBUTTONUP)
        {
            ShowContextMenu();
        }
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        AppendMenu(hMenu, MF_STRING, 1, "Abrir PDFWin");
        AppendMenu(hMenu, MF_STRING, 2, "Activar menú contextual de Windows 11");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, 3, "Salir");

        GetCursorPos(out POINT pt);
        Win32Helper.SetForegroundWindow(_hwnd);

        uint cmd = (uint)TrackPopupMenuEx(hMenu, TPM_RETURNCMD, pt.X, pt.Y, _hwnd, IntPtr.Zero);
        DestroyMenu(hMenu);
        Win32Helper.PostMessage(_hwnd, Win32Helper.WM_NULL, IntPtr.Zero, IntPtr.Zero);

        if (cmd == 1)
        {
            _onOpenWindow();
        }
        else if (cmd == 2)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath))
            {
                ContextMenuService.RegisterContextMenu(exePath);
            }
        }
        else if (cmd == 3)
        {
            _onExit();
        }
    }

    public void Dispose()
    {
        if (_isCreated)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TRAY_ID
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isCreated = false;
        }
    }
}
