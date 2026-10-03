using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SkimStats.Models;

public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight, Custom }

// everything saved to settings.json, observable so the overlay updates live while editing
public partial class AppSettings : ObservableObject
{
    // which stats the overlay shows, and whether each gets a mini graph
    [ObservableProperty] public partial bool ShowCpu { get; set; } = true;
    [ObservableProperty] public partial bool CpuGraph { get; set; }
    [ObservableProperty] public partial bool ShowRam { get; set; } = true;
    [ObservableProperty] public partial bool RamGraph { get; set; }
    [ObservableProperty] public partial bool ShowGpu { get; set; } = true;
    [ObservableProperty] public partial bool GpuGraph { get; set; }

    // temperatures show next to cpu/gpu usage, cpu is opt-in since it needs admin + pawnio
    [ObservableProperty] public partial bool ShowGpuTemp { get; set; } = true;
    [ObservableProperty] public partial bool ShowCpuTemp { get; set; }
    [ObservableProperty] public partial bool ShowDisk { get; set; }
    [ObservableProperty] public partial bool DiskGraph { get; set; }
    [ObservableProperty] public partial bool ShowNetwork { get; set; }
    [ObservableProperty] public partial bool NetworkGraph { get; set; }
    [ObservableProperty] public partial bool ShowFps { get; set; } // off by default, runs presentmon
    [ObservableProperty] public partial bool FpsGraph { get; set; }

    // position, monitor is ignored for custom since the drag already picked the spot
    [ObservableProperty] public partial OverlayCorner Corner { get; set; } = OverlayCorner.TopLeft;
    [ObservableProperty] public partial int MonitorIndex { get; set; }
    [ObservableProperty] public partial int CustomX { get; set; }
    [ObservableProperty] public partial int CustomY { get; set; }

    // overlay width set by dragging its edge, stats wrap into rows to fit
    // 0 = fit to content, which stacks the stats in a list
    [ObservableProperty] public partial double OverlayWidth { get; set; }

    // look
    [ObservableProperty] public partial double FontSize { get; set; } = 14;
    [ObservableProperty] public partial Color TextColor { get; set; } = Colors.White;
    [ObservableProperty] public partial double BackgroundOpacity { get; set; } = 0.6;
    [ObservableProperty] public partial bool ShowOverlayIcon { get; set; } // off by default, keeps the overlay minimal

    [ObservableProperty] public partial Hotkey Hotkey { get; set; } = Hotkey.Default;

    // launch on windows startup isn't here, the registry is the source of truth for that
    [ObservableProperty] public partial bool StartMinimized { get; set; }
}
