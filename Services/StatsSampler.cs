using System;
using Avalonia.Threading;
using SkimStats.Collections;
using SkimStats.Models;

namespace SkimStats.Services;

// reads the provider once per interval and tells every view about it
public sealed class StatsSampler : IDisposable
{
    public const int HistoryLength = 60; // 1 minute at the default 1s interval

    private readonly IStatsProvider _provider;
    // ponytail: runs on the ui thread so views need no marshaling, move to a background timer if reads ever get slow
    private readonly DispatcherTimer _timer;

    public StatsSampler(IStatsProvider provider, TimeSpan interval)
    {
        _provider = provider;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += (_, _) => Sample();
    }

    public event Action<StatsSnapshot>? SnapshotTaken;

    public StatsSnapshot? Latest { get; private set; }
    public RingBuffer<StatsSnapshot> History { get; } = new(HistoryLength);

    public void Start() => _timer.Start();

    private void Sample()
    {
        try
        {
            Latest = _provider.Read();
        }
        catch (Exception ex)
        {
            // skip this tick instead of crashing, counters can fail if perf data is broken
            System.Diagnostics.Trace.WriteLine($"stats read failed: {ex.Message}");
            return;
        }

        History.Add(Latest);
        SnapshotTaken?.Invoke(Latest);
    }

    public void Dispose()
    {
        _timer.Stop();
        _provider.Dispose();
    }
}
