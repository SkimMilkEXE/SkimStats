using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SkimStats.Models;
using SkimStats.Services;
using SkimStats.ViewModels;

namespace SkimStats.Views;

public partial class OverlayWindow : Window
{
    private const int EdgeMargin = 16; // gap from the screen edge in corner mode, in pixels
    private const double MinOverlayWidth = 80;
    private const double ResizeZoneWidth = 20; // wider than the grip so it is easy to grab even as the text width changes

    private IntPtr _hwnd;

    // set while the user drags the resize grip
    private bool _resizing;
    private double _resizeStartWidth;
    private int _resizeStartScreenX;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    private OverlayViewModel ViewModel => (OverlayViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // hwnd only exists once the window is open
        _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (OperatingSystem.IsWindows())
            Win32Interop.MakeOverlay(_hwnd);

        ViewModel.Settings.PropertyChanged += OnSettingChanged;
        ViewModel.PropertyChanged += OnViewModelChanged;

        // size changes when stats or font size change, so corners need re-anchoring
        SizeChanged += (_, _) => ApplyPosition();
        PositionChanged += OnPositionChanged;
        ApplySize();
        ApplyPosition();
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.Corner) or nameof(AppSettings.MonitorIndex))
            ApplyPosition();
        else if (e.PropertyName == nameof(AppSettings.OverlayWidth))
            ApplySize();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OverlayViewModel.IsEditing))
            return;

        // can't drag what you can't see
        if (ViewModel.IsEditing)
            Show();
        if (OperatingSystem.IsWindows())
            Win32Interop.SetClickThrough(_hwnd, !ViewModel.IsEditing);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!ViewModel.IsEditing || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // switch to custom before moving or resizing, so corner re-anchoring doesn't fight the drag
        var settings = ViewModel.Settings;
        settings.CustomX = Position.X;
        settings.CustomY = Position.Y;
        settings.Corner = OverlayCorner.Custom;

        // the rightmost strip (where the blue grip is) resizes, anywhere else moves
        if (e.GetPosition(this).X >= Bounds.Width - ResizeZoneWidth)
        {
            // screen coordinates, since the window itself changes size under the mouse
            _resizing = true;
            _resizeStartWidth = Bounds.Width;
            _resizeStartScreenX = this.PointToScreen(e.GetPosition(this)).X;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // returns right away, the new spot gets saved in OnPositionChanged
        BeginMoveDrag(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_resizing)
            return;

        // screen pixels -> window units, so it tracks the mouse on high dpi screens too
        int screenX = this.PointToScreen(e.GetPosition(this)).X;
        double change = (screenX - _resizeStartScreenX) / DesktopScaling;
        ViewModel.Settings.OverlayWidth = Math.Max(MinOverlayWidth, _resizeStartWidth + change);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_resizing)
            return;
        _resizing = false;
        e.Pointer.Capture(null);
    }

    // fixed width wraps the stats into rows, 0 lets the window fit the stacked list
    private void ApplySize()
    {
        var width = ViewModel.Settings.OverlayWidth;
        if (width > 0)
        {
            SizeToContent = SizeToContent.Height;
            Width = width;
        }
        else
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (!ViewModel.IsEditing || ViewModel.Settings.Corner != OverlayCorner.Custom)
            return;
        ViewModel.Settings.CustomX = e.Point.X;
        ViewModel.Settings.CustomY = e.Point.Y;
    }

    private void ApplyPosition()
    {
        var settings = ViewModel.Settings;

        // custom spot, unless that monitor got unplugged
        if (settings.Corner == OverlayCorner.Custom)
        {
            var custom = new PixelPoint(settings.CustomX, settings.CustomY);
            if (Screens.ScreenFromPoint(custom) is { } customScreen)
            {
                // keep the whole overlay on screen even if it was dragged half off the edge
                var bounds = customScreen.Bounds;
                var customSize = PixelSize.FromSize(Bounds.Size, customScreen.Scaling);
                Position = new PixelPoint(
                    Math.Clamp(custom.X, bounds.X, Math.Max(bounds.X, bounds.Right - customSize.Width)),
                    Math.Clamp(custom.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - customSize.Height)));
                return;
            }
        }

        var screens = Screens.All;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Count
            ? screens[settings.MonitorIndex]
            : Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(Bounds.Size, screen.Scaling);
        int left = area.X + EdgeMargin;
        int top = area.Y + EdgeMargin;
        int right = area.Right - size.Width - EdgeMargin;
        int bottom = area.Bottom - size.Height - EdgeMargin;

        Position = settings.Corner switch
        {
            OverlayCorner.TopRight => new PixelPoint(right, top),
            OverlayCorner.BottomLeft => new PixelPoint(left, bottom),
            OverlayCorner.BottomRight => new PixelPoint(right, bottom),
            _ => new PixelPoint(left, top), // top left, or a custom spot that's gone off screen
        };
    }
}
