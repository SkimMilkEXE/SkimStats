using System;

namespace SkimStats.Models;

// one sample of all stats, taken at the same moment
public record StatsSnapshot(
    DateTime Timestamp,
    float CpuPercent,
    ulong RamUsedBytes,
    ulong RamTotalBytes)
{
    public double RamPercent => RamTotalBytes == 0 ? 0 : 100.0 * RamUsedBytes / RamTotalBytes;
}
