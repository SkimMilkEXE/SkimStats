using CommunityToolkit.Mvvm.ComponentModel;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

public partial class OverlayViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string CpuText { get; set; } = "CPU --";

    [ObservableProperty]
    public partial string RamText { get; set; } = "RAM --";

    public OverlayViewModel(StatsSampler sampler)
    {
        sampler.SnapshotTaken += OnSnapshot;
    }

    // previewer only
    public OverlayViewModel() { }

    private void OnSnapshot(StatsSnapshot s)
    {
        CpuText = $"CPU {s.CpuPercent:0}%";
        RamText = $"RAM {Format.Bytes(s.RamUsedBytes)} / {Format.Bytes(s.RamTotalBytes)}";
    }
}
