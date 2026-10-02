using System;
using System.Collections.Generic;

namespace SkimStats.Models;

// one sample of all stats, taken at the same moment
public record StatsSnapshot(
    DateTime Timestamp,
    float CpuPercent,
    ulong RamUsedBytes,
    ulong RamTotalBytes,
    float DiskActivePercent,
    double DiskReadBytesPerSec,
    double DiskWriteBytesPerSec,
    IReadOnlyList<DriveSpace> Drives,
    double NetDownBytesPerSec,
    double NetUpBytesPerSec)
{
    public double RamPercent => RamTotalBytes == 0 ? 0 : 100.0 * RamUsedBytes / RamTotalBytes;
}

public record DriveSpace(string Name, long UsedBytes, long TotalBytes);
