using System.Globalization;
using SkimStats.ViewModels;

namespace SkimStats.Tests;

public class FormatTests
{
    public FormatTests()
    {
        // pin the decimal separator so "1.5" doesn't become "1,5" on other machines
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024 * 1024, "1.0 MB")]
    [InlineData(1024L * 1024 * 1024 * 64, "64.0 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024 * 4, "4.0 TB")]
    public void BytesPicksTheRightUnit(double bytes, string expected)
    {
        Assert.Equal(expected, Format.Bytes(bytes));
    }

    [Fact]
    public void BytesStopsAtTerabytes()
    {
        // 2048 TB has no bigger unit to move to
        Assert.Equal("2048.0 TB", Format.Bytes(1024.0 * 1024 * 1024 * 1024 * 2048));
    }

    [Fact]
    public void RateAddsPerSecond()
    {
        Assert.Equal("1.5 MB/s", Format.Rate(1.5 * 1024 * 1024));
    }
}
