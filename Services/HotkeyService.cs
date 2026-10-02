using System;
using System.Runtime.Versioning;
using Avalonia.Controls;
using SkimStats.Models;

namespace SkimStats.Services;

// one system-wide hotkey that works even while a game has focus
[SupportedOSPlatform("windows")]
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private readonly IntPtr _hwnd;

    public event Action? Pressed;

    // null when nothing is registered (another app owns the combo, or mid re-recording)
    public Hotkey? Current { get; private set; }

    // window must stay alive (hidden is fine) since windows sends the key press to it
    public HotkeyService(Window window)
    {
        _hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Win32Properties.AddWndProcHookCallback(window, OnMessage);
    }

    // swaps to a new combo, false when another app already owns it
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        if (!Win32Interop.RegisterHotKey(_hwnd, HotkeyId, hotkey.Modifiers | Win32Interop.MOD_NOREPEAT, hotkey.VirtualKey))
            return false;
        Current = hotkey;
        return true;
    }

    public void Unregister()
    {
        if (Current is null)
            return;
        Win32Interop.UnregisterHotKey(_hwnd, HotkeyId);
        Current = null;
    }

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY && wParam == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
