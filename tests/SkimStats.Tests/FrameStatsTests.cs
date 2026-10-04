using System.Globalization;
using System.Runtime.Versioning;
using SkimStats.Services;
using SkimStats.ViewModels;

namespace SkimStats.Tests;

[SupportedOSPlatform("windows")]
public class FrameStatsTests
{
    public FrameStatsTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    // frames spaced by their own frame time, like presentmon reports them
    private static List<(double TimeMs, double FrameMs)> Frames(IEnumerable<double> frameTimes)
    {
        var frames = new List<(double, double)>();
        double time = 0;
        foreach (var ms in frameTimes)
        {
            time += ms;
            frames.Add((time, ms));
        }
        return frames;
    }

    [Fact]
    public void SteadyFramesGiveSameAverageAndLow()
    {
        var times = Enumerable.Repeat(1000.0 / 144, 1440).ToList();

        Assert.Equal(144, FpsMonitor.OnePercentLowFps(times), precision: 6);
    }

    [Fact]
    public void StuttersPullTheLowDownButBarelyMoveTheAverage()
    {
        // 990 smooth frames at 7ms and 10 stutters at 20ms
        var times = Enumerable.Repeat(7.0, 990).Concat(Enumerable.Repeat(20.0, 10)).ToList();

        Assert.Equal(50, FpsMonitor.OnePercentLowFps(times), precision: 6);
        Assert.True(FpsMonitor.AverageFps(times) > 135);
    }

    [Fact]
    public void LowWithNoFramesIsZero()
    {
        Assert.Equal(0, FpsMonitor.OnePercentLowFps([]));
    }

    [Fact]
    public void StatsUseLastSecondForFpsAndEverythingForTheLow()
    {
        // 9 seconds with an early stutter, then a smooth last second
        var times = Enumerable.Repeat(10.0, 100).Append(100.0).Concat(Enumerable.Repeat(10.0, 790)).Concat(Enumerable.Repeat(10.0, 100));

        var stats = FpsMonitor.ComputeStats(Frames(times));

        Assert.Equal(100, stats.Fps, precision: 6);              // last second is smooth
        Assert.Equal(10, stats.FrameTimeMs, precision: 6);
        Assert.Equal(10, stats.WorstFrameTimeMs, precision: 6);   // stutter isn't in the last second
        // slowest 1% of 991 frames = 10 frames: the 100ms hitch + nine 10ms ones -> 190ms for 10 frames
        Assert.Equal(1000.0 * 10 / 190, stats.OnePercentLowFps, precision: 6);
    }

    [Fact]
    public void WorstFrameTimeCatchesASpikeInTheLastSecond()
    {
        var times = Enumerable.Repeat(10.0, 50).Append(80.0).Concat(Enumerable.Repeat(10.0, 40));

        Assert.Equal(80, FpsMonitor.ComputeStats(Frames(times)).WorstFrameTimeMs);
    }

    [Theory]
    [InlineData(true, false, "FPS 144 · 1% 97")]
    [InlineData(true, true, "FPS 144 · 1% 97 · 6.9 ms")]
    [InlineData(false, true, "FPS 144 · 6.9 ms")]
    [InlineData(false, false, "FPS 144")]
    public void FpsLineShowsChosenParts(bool showLow, bool showFrameTime, string expected)
    {
        var stats = new FrameStats(144.2, 97.4, 6.94, 12);

        Assert.Equal(expected, Format.FpsLine(stats, showLow, showFrameTime));
    }

    [Fact]
    public void FpsLineWithoutFramesShowsDashes()
    {
        Assert.Equal("FPS --", Format.FpsLine(null, true, true));
    }
}
