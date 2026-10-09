using System;
using PDFWin.Native;

namespace PDFWin.Services;

public class HotkeyService : IDisposable
{
    public const int HOTKEY_ID = 9001;
    private const uint VK_P = 0x50; // 'P' key

    private readonly IntPtr _hwnd;
    private bool _isRegistered = false;

    public HotkeyService(IntPtr hwnd)
    {
        _hwnd = hwnd;
        Register();
    }

    public bool Register()
    {
        if (_isRegistered) return true;

        uint modifiers = Win32Helper.MOD_WIN | Win32Helper.MOD_ALT | Win32Helper.MOD_NOREPEAT;
        _isRegistered = Win32Helper.RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, VK_P);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
    }
}
