using CommunityToolkit.Mvvm.ComponentModel;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string CpuText { get; set; } = "CPU: --";

    [ObservableProperty]
    public partial string RamText { get; set; } = "RAM: --";

    public MainViewModel(StatsSampler sampler)
    {
        sampler.SnapshotTaken += OnSnapshot;
    }

    // used when stats couldn't start, shows the reason instead of numbers
    public MainViewModel(string error)
    {
        CpuText = error;
        RamText = "";
    }

    // previewer only
    public MainViewModel() { }

    private void OnSnapshot(StatsSnapshot s)
    {
        const double gb = 1024.0 * 1024 * 1024;
        CpuText = $"CPU: {s.CpuPercent:0}%";
        RamText = $"RAM: {s.RamUsedBytes / gb:0.0} / {s.RamTotalBytes / gb:0.0} GB ({s.RamPercent:0}%)";
    }
}
