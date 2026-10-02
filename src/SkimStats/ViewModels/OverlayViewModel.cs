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
    [ObservableProperty] public partial string DiskText { get; set; } = "DISK --";
    [ObservableProperty] public partial string NetworkText { get; set; } = "NET --";
    [ObservableProperty] public partial string FpsText { get; set; } = "FPS --";

    // sparkline data, only filled for stats that are shown with a graph
    [ObservableProperty] public partial double[]? CpuHistory { get; set; }
    [ObservableProperty] public partial double[]? RamHistory { get; set; }
    [ObservableProperty] public partial double[]? DiskHistory { get; set; }
    [ObservableProperty] public partial double[]? NetworkHistory { get; set; }
    [ObservableProperty] public partial double[]? FpsHistory { get; set; }

    // true while the user is dragging the overlay into place
    [ObservableProperty] public partial bool IsEditing { get; set; }

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
        };
    }

    private void OnSnapshot(StatsSnapshot s)
    {
        CpuText = $"CPU {s.CpuPercent:0}%";
        RamText = $"RAM {Format.Bytes(s.RamUsedBytes)} / {Format.Bytes(s.RamTotalBytes)}";
        DiskText = $"DISK {s.DiskActivePercent:0}%";
        NetworkText = $"NET ↓{Format.Rate(s.NetDownBytesPerSec)} ↑{Format.Rate(s.NetUpBytesPerSec)}";
        FpsText = s.Fps is { } fps ? $"FPS {fps:0}" : "FPS --";

        CpuHistory = HistoryIf(Settings.ShowCpu && Settings.CpuGraph, x => x.CpuPercent);
        RamHistory = HistoryIf(Settings.ShowRam && Settings.RamGraph, x => x.RamPercent);
        DiskHistory = HistoryIf(Settings.ShowDisk && Settings.DiskGraph, x => x.DiskActivePercent);
        // ponytail: download only, add an upload line if people ask for it
        NetworkHistory = HistoryIf(Settings.ShowNetwork && Settings.NetworkGraph, x => x.NetDownBytesPerSec);
        FpsHistory = HistoryIf(Settings.ShowFps && Settings.FpsGraph, x => x.Fps ?? 0);
    }

    // skips the copy entirely when the graph is hidden
    private double[]? HistoryIf(bool wanted, Func<StatsSnapshot, double> pick) =>
        wanted && _sampler is not null ? _sampler.History.Select(pick).ToArray() : null;
}
