using System;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

public partial class OverlayViewModel : ViewModelBase
{
    private readonly StatsSampler? _sampler;

    public AppSettings Settings { get; }

    [ObservableProperty] public partial string CpuText { get; set; } = "CPU --";
    [ObservableProperty] public partial string RamText { get; set; } = "RAM --";
    [ObservableProperty] public partial string GpuText { get; set; } = "GPU --";
    [ObservableProperty] public partial string DiskText { get; set; } = "DISK --";
    [ObservableProperty] public partial string NetworkText { get; set; } = "NET --";
    [ObservableProperty] public partial string FpsText { get; set; } = "FPS --";

    // sparkline data, only filled for stats that are shown with a graph
    [ObservableProperty] public partial double[]? CpuHistory { get; set; }
    [ObservableProperty] public partial double[]? RamHistory { get; set; }
    [ObservableProperty] public partial double[]? GpuHistory { get; set; }
    [ObservableProperty] public partial double[]? DiskHistory { get; set; }
    [ObservableProperty] public partial double[]? NetworkHistory { get; set; }
    [ObservableProperty] public partial double[]? FpsHistory { get; set; }

    // true while the user is dragging the overlay into place
    [ObservableProperty] public partial bool IsEditing { get; set; }

    // list when fitting to content, rows that wrap once the user picks a width
    public bool HasFixedWidth => Settings.OverlayWidth > 0;
    public Avalonia.Layout.Orientation StatsOrientation =>
        HasFixedWidth ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical;
    public double StatsSpacing => HasFixedWidth ? 16 : 2; // gap between stats, wider when side by side

    public IBrush TextBrush => new SolidColorBrush(Settings.TextColor);
    public IBrush BackgroundBrush => new SolidColorBrush(Colors.Black, Settings.BackgroundOpacity);

    public OverlayViewModel(StatsSampler sampler, AppSettings settings) : this(settings)
    {
        _sampler = sampler;
        sampler.SnapshotTaken += OnSnapshot;
    }

    // previewer only
    public OverlayViewModel() : this(new AppSettings()) { }

    private OverlayViewModel(AppSettings settings)
    {
        Settings = settings;
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.TextColor))
                OnPropertyChanged(nameof(TextBrush));
            else if (e.PropertyName == nameof(AppSettings.BackgroundOpacity))
                OnPropertyChanged(nameof(BackgroundBrush));
            else if (e.PropertyName == nameof(AppSettings.OverlayWidth))
            {
                OnPropertyChanged(nameof(HasFixedWidth));
                OnPropertyChanged(nameof(StatsOrientation));
                OnPropertyChanged(nameof(StatsSpacing));
            }
        };
    }

    private void OnSnapshot(StatsSnapshot s)
    {
        CpuText = $"CPU {Format.UsageWithTemp(s.CpuPercent, s.CpuTempC, Settings.ShowCpuTemp)}";
        RamText = $"RAM {Format.Bytes(s.RamUsedBytes)} / {Format.Bytes(s.RamTotalBytes)}";
        GpuText = $"GPU {Format.UsageWithTemp(s.GpuPercent, s.GpuTempC, Settings.ShowGpuTemp)}";
        DiskText = $"DISK {s.DiskActivePercent:0}%";
        NetworkText = $"NET ↓{Format.Rate(s.NetDownBytesPerSec)} ↑{Format.Rate(s.NetUpBytesPerSec)}";
        FpsText = Format.FpsLine(s.Frames, Settings.ShowFpsLow, Settings.ShowFrameTime);

        CpuHistory = HistoryIf(Settings.ShowCpu && Settings.CpuGraph, x => x.CpuPercent);
        RamHistory = HistoryIf(Settings.ShowRam && Settings.RamGraph, x => x.RamPercent);
        GpuHistory = HistoryIf(Settings.ShowGpu && Settings.GpuGraph, x => x.GpuPercent ?? 0);
        DiskHistory = HistoryIf(Settings.ShowDisk && Settings.DiskGraph, x => x.DiskActivePercent);
        // ponytail: download only, add an upload line if people ask for it
        NetworkHistory = HistoryIf(Settings.ShowNetwork && Settings.NetworkGraph, x => x.NetDownBytesPerSec);
        // frame time graph uses each second's worst frame, so a single stutter shows as a spike
        FpsHistory = HistoryIf(Settings.ShowFps && Settings.FpsGraph,
            x => Settings.FpsGraphFrameTime ? x.Frames?.WorstFrameTimeMs ?? 0 : x.Frames?.Fps ?? 0);
    }

    // skips the copy entirely when the graph is hidden
    private double[]? HistoryIf(bool wanted, Func<StatsSnapshot, double> pick) =>
        wanted && _sampler is not null ? _sampler.History.Select(pick).ToArray() : null;
}
