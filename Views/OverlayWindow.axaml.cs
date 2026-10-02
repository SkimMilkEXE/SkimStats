using System;
using Avalonia;
using Avalonia.Controls;
using SkimStats.Services;

namespace SkimStats.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // top left corner of the main screen for now, settings come in milestone 6
        if (Screens.Primary is { } screen)
            Position = screen.WorkingArea.TopLeft + new PixelVector(16, 16);

        // hwnd only exists once the window is open
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
            Win32Interop.MakeClickThrough(handle.Handle);
    }
}
