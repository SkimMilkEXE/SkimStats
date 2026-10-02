using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkimStats.Models;

namespace SkimStats.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsStatsProvider : IStatsProvider
{
    // matches task manager better than "Processor / % Processor Time"
    private readonly PerformanceCounter _cpuCounter = new("Processor Information", "% Processor Utility", "_Total");

    public WindowsStatsProvider()
    {
        // first read always returns 0, throw it away
        _cpuCounter.NextValue();
    }

    public StatsSnapshot Read()
    {
        // utility can go past 100 when the cpu boosts above base clock
        var cpu = Math.Clamp(_cpuCounter.NextValue(), 0f, 100f);

        var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref mem))
            mem = default; // shows 0 instead of crashing

        return new StatsSnapshot(DateTime.Now, cpu, mem.TotalPhys - mem.AvailPhys, mem.TotalPhys);
    }

    public void Dispose() => _cpuCounter.Dispose();

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
