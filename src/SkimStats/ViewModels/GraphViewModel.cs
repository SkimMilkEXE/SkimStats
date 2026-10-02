using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using SkimStats.Services;

namespace SkimStats.ViewModels;

// one titled live graph with one or more lines
public partial class GraphViewModel : ViewModelBase
{
    // graph history, the chart redraws when these change
    private readonly ObservableCollection<double>[] _values;

    [ObservableProperty]
    public partial string Text { get; set; }

    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    // legend only helps when there's more than one line
    public LegendPosition LegendPosition => Series.Length > 1 ? LegendPosition.Top : LegendPosition.Hidden;

    // maxY null means the y axis scales to fit the data
    public GraphViewModel(string text, Func<double, string> labeler, double? maxY, params string[] lineNames)
    {
        Text = text;
        _values = lineNames.Select(_ => new ObservableCollection<double>()).ToArray();

        // plain lines, no dots or curve smoothing
        Series = lineNames.Select((name, i) => (ISeries)new LineSeries<double>
        {
            Name = name,
            Values = _values[i],
            GeometrySize = 0,
            LineSmoothness = 0,
        }).ToArray();

        // hidden x axis fixed to the full history width, so the line doesn't stretch while it fills up
        XAxes = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = StatsSampler.HistoryLength - 1 }];
        YAxes = [new Axis { MinLimit = 0, MaxLimit = maxY, Labeler = labeler }];
    }

    // one value per line, in the same order as the line names
    public void Add(params double[] values)
    {
        for (int i = 0; i < _values.Length; i++)
        {
            _values[i].Add(values[i]);
            // keeps the graph at a fixed width by dropping the oldest point
            if (_values[i].Count > StatsSampler.HistoryLength)
                _values[i].RemoveAt(0);
        }
    }
}
