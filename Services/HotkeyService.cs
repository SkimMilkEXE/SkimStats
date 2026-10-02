using System;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace SkimStats.Services;

// one system-wide hotkey that works even while a game has focus
[SupportedOSPlatform("windows")]
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private readonly IntPtr _hwnd;

    public event Action? Pressed;

    // false when another app already owns this key combo
    public bool IsRegistered { get; }

    // window must stay alive (hidden is fine) since windows sends the key press to it
    public HotkeyService(Window window, uint modifiers, uint virtualKey)
    {
        _hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Win32Properties.AddWndProcHookCallback(window, OnMessage);
        IsRegistered = Win32Interop.RegisterHotKey(_hwnd, HotkeyId, modifiers | Win32Interop.MOD_NOREPEAT, virtualKey);
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

    public void Dispose() => Win32Interop.UnregisterHotKey(_hwnd, HotkeyId);
}
