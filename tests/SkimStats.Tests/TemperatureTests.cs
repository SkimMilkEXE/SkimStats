using System.Globalization;
using System.Runtime.Versioning;
using SkimStats.Services;
using SkimStats.ViewModels;

namespace SkimStats.Tests;

[SupportedOSPlatform("windows")]
public class TemperatureTests
{
    public TemperatureTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void GpuPicksTheHottestAdapter()
    {
        Assert.Equal(71.5, GpuTemperatureReader.Hottest([45.0, 71.5]));
    }

    [Fact]
    public void GpuIgnoresAdaptersReportingZero()
    {
        // software renderers and old drivers report 0, that isn't a real temperature
        Assert.Equal(45.0, GpuTemperatureReader.Hottest([0, 45.0]));
        Assert.Null(GpuTemperatureReader.Hottest([0, 0]));
        Assert.Null(GpuTemperatureReader.Hottest([]));
    }

    [Fact]
    public void CpuPrefersAmdTctlSensor()
    {
        var temp = CpuTemperatureReader.PickCpuTemperature(
        [
            ("Core #1", 70f),
            ("Core (Tctl/Tdie)", 62.5f),
            ("CCD1 (Tdie)", 58f),
        ]);

        Assert.Equal(62.5, temp);
    }

    [Fact]
    public void CpuPrefersIntelPackageSensor()
    {
        var temp = CpuTemperatureReader.PickCpuTemperature(
        [
            ("CPU Core #1", 75f),
            ("CPU Package", 68f),
        ]);

        Assert.Equal(68, temp);
    }

    [Fact]
    public void CpuFallsBackToHottestSensor()
    {
        Assert.Equal(70, CpuTemperatureReader.PickCpuTemperature([("Core #1", 70f), ("Core #2", 66f)]));
    }

    [Fact]
    public void CpuWithoutReadableSensorsIsNull()
    {
        // what you get without admin or pawnio: sensors exist but read 0 or nothing
        Assert.Null(CpuTemperatureReader.PickCpuTemperature([("Core (Tctl/Tdie)", 0f), ("CCD1", null)]));
        Assert.Null(CpuTemperatureReader.PickCpuTemperature([]));
    }

    [Theory]
    [InlineData(21.4, 62.6, true, "21% · 63°C")]
    [InlineData(21.4, 62.6, false, "21%")]
    [InlineData(21.4, null, true, "21%")]
    [InlineData(null, 45.0, true, "-- · 45°C")]
    [InlineData(null, null, true, "--")]
    public void UsageWithTempFormatsBothParts(double? percent, double? tempC, bool showTemp, string expected)
    {
        Assert.Equal(expected, Format.UsageWithTemp(percent, tempC, showTemp));
    }
}
