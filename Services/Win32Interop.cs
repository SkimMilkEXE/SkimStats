using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SkimStats.Services;

// raw win32 calls avalonia doesn't expose
[SupportedOSPlatform("windows")]
public static class Win32Interop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x20;     // mouse goes through to the window below
    private const long WS_EX_TOOLWINDOW = 0x80;      // hidden from alt-tab
    private const long WS_EX_LAYERED = 0x80000;      // needed for WS_EX_TRANSPARENT to pass clicks through
    private const long WS_EX_NOACTIVATE = 0x8000000; // never steals focus from the game

    // sets up the overlay window: layered, hidden from alt-tab, never focused, and click-through
    public static void MakeOverlay(IntPtr hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);

        // a layered window draws nothing until it gets an opacity, 255 = fully opaque
        // (the overlay's own transparency still comes from avalonia)
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    // edit mode turns this off so the overlay can be dragged, then back on to lock it
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style = clickThrough ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);
    }

    // 64-bit only, fine since we ship win-x64
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    // global hotkeys, windows sends WM_HOTKEY to hwnd when pressed anywhere
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000; // holding the keys down doesn't spam the event

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
