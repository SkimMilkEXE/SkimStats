using System.Runtime.Versioning;
using SkimStats.Services;

namespace SkimStats.Tests;

[SupportedOSPlatform("windows")]
public class FpsMonitorTests
{
    [Fact]
    public void SteadyFrameTimesGiveMatchingFps()
    {
        var frames = Enumerable.Repeat(1000.0 / 60, 60).ToList();

        Assert.Equal(60, FpsMonitor.AverageFps(frames), precision: 6);
    }

    [Fact]
    public void UsesAverageFrameTimeNotAverageOfFps()
    {
        // one 10ms and one 30ms frame = 2 frames in 40ms = 50 fps
        // (averaging 100 fps and 33 fps would wrongly say ~67)
        Assert.Equal(50, FpsMonitor.AverageFps([10.0, 30.0]), precision: 6);
    }

    [Fact]
    public void HighRefreshRateWorks()
    {
        var frames = Enumerable.Repeat(1000.0 / 240, 240).ToList();

        Assert.Equal(240, FpsMonitor.AverageFps(frames), precision: 6);
    }

    [Fact]
    public void NoFramesIsZeroNotDivideByZero()
    {
        Assert.Equal(0, FpsMonitor.AverageFps([]));
    }
}
