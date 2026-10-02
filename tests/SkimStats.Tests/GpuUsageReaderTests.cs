using System.Runtime.Versioning;
using SkimStats.Services;

namespace SkimStats.Tests;

[SupportedOSPlatform("windows")]
public class GpuUsageReaderTests
{
    private const string Gpu = "luid_0x00000000_0x5a241b4f_phys_0";
    private const string IGpu = "luid_0x00000000_0x00010c0a_phys_0";

    [Fact]
    public void AddsUpProcessesOnTheSameEngine()
    {
        // game + browser both on the 3d engine
        var usage = GpuUsageReader.BusiestEngine(
        [
            ($"pid_100_{Gpu}_eng_0_engtype_3d", 60),
            ($"pid_200_{Gpu}_eng_0_engtype_3d", 25),
        ]);

        Assert.Equal(85, usage);
    }

    [Fact]
    public void ReportsTheBusiestEngineNotTheSum()
    {
        // 3d at 70% and video decode at 40% is a 70% busy gpu, not 110%
        var usage = GpuUsageReader.BusiestEngine(
        [
            ($"pid_100_{Gpu}_eng_0_engtype_3d", 70),
            ($"pid_100_{Gpu}_eng_2_engtype_videodecode", 40),
        ]);

        Assert.Equal(70, usage);
    }

    [Fact]
    public void PicksTheBusiestGpuWhenThereAreTwo()
    {
        // integrated graphics barely used, graphics card doing the work
        var usage = GpuUsageReader.BusiestEngine(
        [
            ($"pid_100_{IGpu}_eng_0_engtype_3d", 5),
            ($"pid_200_{Gpu}_eng_0_engtype_3d", 92),
        ]);

        Assert.Equal(92, usage);
    }

    [Fact]
    public void CapsAtOneHundred()
    {
        // per process rounding can push the total a bit over 100
        var usage = GpuUsageReader.BusiestEngine(
        [
            ($"pid_100_{Gpu}_eng_0_engtype_3d", 70),
            ($"pid_200_{Gpu}_eng_0_engtype_3d", 35),
        ]);

        Assert.Equal(100, usage);
    }

    [Fact]
    public void NoCountersIsZero()
    {
        Assert.Equal(0, GpuUsageReader.BusiestEngine([]));
    }
}
