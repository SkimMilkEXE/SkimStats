using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // graph history, the chart redraws when these change
    private readonly ObservableCollection<double> _cpuValues = [];
    private readonly ObservableCollection<double> _ramValues = [];

    [ObservableProperty]
    public partial string CpuText { get; set; } = "CPU: --";

    [ObservableProperty]
    public partial string RamText { get; set; } = "RAM: --";

    public ISeries[] CpuSeries { get; }
    public ISeries[] RamSeries { get; }

    // each chart needs its own axis objects, they can't be shared
    public Axis[] CpuXAxes { get; } = [TimeAxis()];
    public Axis[] CpuYAxes { get; } = [PercentAxis()];
    public Axis[] RamXAxes { get; } = [TimeAxis()];
    public Axis[] RamYAxes { get; } = [PercentAxis()];

    public MainViewModel(StatsSampler sampler) : this()
    {
        sampler.SnapshotTaken += OnSnapshot;
    }

    // used when stats couldn't start, shows the reason instead of numbers
    public MainViewModel(string error) : this()
    {
        CpuText = error;
        RamText = "";
    }

    // previewer only
    public MainViewModel()
    {
        CpuSeries = [LineOf(_cpuValues)];
        RamSeries = [LineOf(_ramValues)];
    }

    private void OnSnapshot(StatsSnapshot s)
    {
        const double gb = 1024.0 * 1024 * 1024;
        CpuText = $"CPU: {s.CpuPercent:0}%";
        RamText = $"RAM: {s.RamUsedBytes / gb:0.0} / {s.RamTotalBytes / gb:0.0} GB ({s.RamPercent:0}%)";

        AddCapped(_cpuValues, s.CpuPercent);
        AddCapped(_ramValues, s.RamPercent);
    }

    // keeps the graph at a fixed width by dropping the oldest point
    private static void AddCapped(ObservableCollection<double> values, double value)
    {
        values.Add(value);
        if (values.Count > StatsSampler.HistoryLength)
            values.RemoveAt(0);
    }

    // plain line, no dots or curve smoothing
    private static LineSeries<double> LineOf(ObservableCollection<double> values) => new()
    {
        Values = values,
        GeometrySize = 0,
        LineSmoothness = 0,
    };

    // hidden x axis fixed to the full history width, so the line doesn't stretch while it fills up
    private static Axis TimeAxis() => new()
    {
        IsVisible = false,
        MinLimit = 0,
        MaxLimit = StatsSampler.HistoryLength - 1,
    };

    private static Axis PercentAxis() => new()
    {
        MinLimit = 0,
        MaxLimit = 100,
        Labeler = v => $"{v:0}%",
    };
}
