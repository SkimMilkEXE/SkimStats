using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkimStats.Models;

namespace SkimStats.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsStatsProvider : IStatsProvider
{
    // matches task manager better than "Processor / % Processor Time"
    private readonly PerformanceCounter _cpuCounter = new("Processor Information", "% Processor Utility", "_Total");

    // all physical disks combined
    private readonly PerformanceCounter _diskIdleCounter = new("PhysicalDisk", "% Idle Time", "_Total");
    private readonly PerformanceCounter _diskReadCounter = new("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
    private readonly PerformanceCounter _diskWriteCounter = new("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

    private readonly NetworkRateTracker _network = new();
    private readonly GpuUsageReader _gpu = new();
    private readonly GpuTemperatureReader _gpuTemp = new();
    private readonly FpsMonitor _fps;
    private readonly CpuTemperatureReader _cpuTemp;

    public WindowsStatsProvider(FpsMonitor fps, CpuTemperatureReader cpuTemp)
    {
        _fps = fps;
        _cpuTemp = cpuTemp;

        // first read of rate counters always returns 0, throw it away
        _cpuCounter.NextValue();
        _diskIdleCounter.NextValue();
        _diskReadCounter.NextValue();
        _diskWriteCounter.NextValue();
    }

    public StatsSnapshot Read()
    {
        // utility can go past 100 when the cpu boosts above base clock
        var cpu = Math.Clamp(_cpuCounter.NextValue(), 0f, 100f);

        var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref mem))
            mem = default; // shows 0 instead of crashing

        // "% Disk Time" goes way past 100 with queued requests, idle time is closer to task manager
        var diskActive = Math.Clamp(100f - _diskIdleCounter.NextValue(), 0f, 100f);

        // fixed drives only, network and removable drives can hang or vanish
        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DriveSpace(d.Name, d.TotalSize - d.TotalFreeSpace, d.TotalSize))
            .ToList();

        var (down, up) = _network.Read();

        return new StatsSnapshot(
            DateTime.Now,
            cpu,
            _cpuTemp.Read(),
            mem.TotalPhys - mem.AvailPhys,
            mem.TotalPhys,
            _gpu.Read(),
            _gpuTemp.Read(),
            diskActive,
            _diskReadCounter.NextValue(),
            _diskWriteCounter.NextValue(),
            drives,
            down,
            up,
            _fps.Status == FpsStatus.Running ? _fps.ForegroundFps() : null);
    }

    public void Dispose()
    {
        _cpuCounter.Dispose();
        _diskIdleCounter.Dispose();
        _diskReadCounter.Dispose();
        _diskWriteCounter.Dispose();
        _gpuTemp.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
