using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SkimStats.Services;

// turns the os byte totals into bytes per second by diffing against the last read
// works on windows and linux
public sealed class NetworkRateTracker
{
    // last totals per adapter, so adapters appearing or vanishing (vpn, wifi) don't cause spikes
    private Dictionary<string, (long Received, long Sent)> _last = [];
    private readonly Stopwatch _sinceLast = Stopwatch.StartNew();

    public NetworkRateTracker()
    {
        // first read only sets the baseline
        Read();
    }

    public (double DownPerSec, double UpPerSec) Read()
    {
        var now = new Dictionary<string, (long Received, long Sent)>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            try
            {
                var stats = nic.GetIPStatistics();
                now[nic.Id] = (stats.BytesReceived, stats.BytesSent);
            }
            catch (NetworkInformationException)
            {
                // some virtual adapters refuse stats, just skip them
            }
        }

        var seconds = _sinceLast.Elapsed.TotalSeconds;
        _sinceLast.Restart();

        var (down, up) = Rates(_last, now, seconds);
        _last = now;
        return (down, up);
    }

    // only adapters seen both times count, negative diffs (counter reset) count as 0
    public static (double DownPerSec, double UpPerSec) Rates(
        IReadOnlyDictionary<string, (long Received, long Sent)> before,
        IReadOnlyDictionary<string, (long Received, long Sent)> after,
        double seconds)
    {
        if (seconds <= 0)
            return (0, 0);

        long received = 0, sent = 0;
        foreach (var (id, totals) in after)
        {
            if (!before.TryGetValue(id, out var old))
                continue;
            received += Math.Max(0, totals.Received - old.Received);
            sent += Math.Max(0, totals.Sent - old.Sent);
        }

        return (received / seconds, sent / seconds);
    }
}
