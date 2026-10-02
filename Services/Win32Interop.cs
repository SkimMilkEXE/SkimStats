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

    // makes the window ignore the mouse completely, clicks land on whatever is underneath
    public static void MakeClickThrough(IntPtr hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);

        // a layered window draws nothing until it gets an opacity, 255 = fully opaque
        // (the overlay's own transparency still comes from avalonia)
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    // 64-bit only, fine since we ship win-x64
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
