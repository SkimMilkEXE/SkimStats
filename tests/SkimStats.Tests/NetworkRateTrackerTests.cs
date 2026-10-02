using SkimStats.Services;

namespace SkimStats.Tests;

public class NetworkRateTrackerTests
{
    private static Dictionary<string, (long Received, long Sent)> Totals(params (string Id, long Received, long Sent)[] adapters) =>
        adapters.ToDictionary(a => a.Id, a => (a.Received, a.Sent));

    [Fact]
    public void DividesByteDiffByElapsedTime()
    {
        var before = Totals(("eth", 1_000, 500));
        var after = Totals(("eth", 3_000, 1_500));

        var (down, up) = NetworkRateTracker.Rates(before, after, seconds: 2);

        Assert.Equal(1_000, down);
        Assert.Equal(500, up);
    }

    [Fact]
    public void AddsUpAllAdapters()
    {
        var before = Totals(("eth", 0, 0), ("wifi", 0, 0));
        var after = Totals(("eth", 100, 10), ("wifi", 50, 5));

        var (down, up) = NetworkRateTracker.Rates(before, after, seconds: 1);

        Assert.Equal(150, down);
        Assert.Equal(15, up);
    }

    [Fact]
    public void IgnoresAdapterThatJustAppeared()
    {
        // a vpn connecting shows up with huge lifetime totals, that's not traffic from the last second
        var before = Totals(("eth", 100, 100));
        var after = Totals(("eth", 200, 200), ("vpn", 9_000_000, 9_000_000));

        var (down, up) = NetworkRateTracker.Rates(before, after, seconds: 1);

        Assert.Equal(100, down);
        Assert.Equal(100, up);
    }

    [Fact]
    public void IgnoresAdapterThatDisappeared()
    {
        var before = Totals(("eth", 100, 100), ("vpn", 500, 500));
        var after = Totals(("eth", 300, 300));

        var (down, up) = NetworkRateTracker.Rates(before, after, seconds: 1);

        Assert.Equal(200, down);
        Assert.Equal(200, up);
    }

    [Fact]
    public void CounterResetCountsAsZeroNotNegative()
    {
        var before = Totals(("eth", 5_000, 5_000));
        var after = Totals(("eth", 100, 100));

        var (down, up) = NetworkRateTracker.Rates(before, after, seconds: 1);

        Assert.Equal(0, down);
        Assert.Equal(0, up);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoElapsedTimeReturnsZeroInsteadOfDividingByZero(double seconds)
    {
        var (down, up) = NetworkRateTracker.Rates(Totals(("eth", 0, 0)), Totals(("eth", 100, 100)), seconds);

        Assert.Equal(0, down);
        Assert.Equal(0, up);
    }
}
