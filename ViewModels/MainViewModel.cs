using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public GraphViewModel Cpu { get; } = new("CPU: --", Percent, 100, "CPU");
    public GraphViewModel Ram { get; } = new("RAM: --", Percent, 100, "RAM");
    public GraphViewModel Disk { get; } = new("Disk: --", Percent, 100, "Active");
    public GraphViewModel Network { get; } = new("Network: --", Format.Rate, null, "Down", "Up");

    // free space per drive, changes slowly so it's text not a graph
    [ObservableProperty]
    public partial string DrivesText { get; set; } = "";

    // set when stats couldn't start, shows the reason instead of graphs
    [ObservableProperty]
    public partial string? Error { get; set; }

    public MainViewModel(StatsSampler sampler)
    {
        sampler.SnapshotTaken += OnSnapshot;
    }

    public MainViewModel(string error)
    {
        Error = error;
    }

    // previewer only
    public MainViewModel() { }

    private static string Percent(double value) => $"{value:0}%";

    private void OnSnapshot(StatsSnapshot s)
    {
        Cpu.Text = $"CPU: {s.CpuPercent:0}%";
        Cpu.Add(s.CpuPercent);

        Ram.Text = $"RAM: {Format.Bytes(s.RamUsedBytes)} / {Format.Bytes(s.RamTotalBytes)} ({s.RamPercent:0}%)";
        Ram.Add(s.RamPercent);

        Disk.Text = $"Disk: {s.DiskActivePercent:0}%   Read {Format.Rate(s.DiskReadBytesPerSec)}   Write {Format.Rate(s.DiskWriteBytesPerSec)}";
        Disk.Add(s.DiskActivePercent);

        Network.Text = $"Network: Down {Format.Rate(s.NetDownBytesPerSec)}   Up {Format.Rate(s.NetUpBytesPerSec)}";
        Network.Add(s.NetDownBytesPerSec, s.NetUpBytesPerSec);

        DrivesText = string.Join("     ", s.Drives.Select(d =>
            $"{d.Name.TrimEnd('\\')} {Format.Bytes(d.UsedBytes)} / {Format.Bytes(d.TotalBytes)} used"));
    }
}
