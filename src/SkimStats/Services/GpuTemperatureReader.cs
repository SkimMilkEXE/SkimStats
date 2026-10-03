using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SkimStats.Services;

// gpu temperature straight from the graphics driver, the same source task manager uses
// works for nvidia, amd and intel without admin, about 0.2ms per read
[SupportedOSPlatform("windows")]
public sealed class GpuTemperatureReader : IDisposable
{
    private const int KMTQAITYPE_ADAPTERPERFDATA = 62;
    private readonly List<uint> _adapters = [];

    public GpuTemperatureReader()
    {
        // first call fills in how many adapters there are, second call fills in the adapters
        var request = new EnumAdapters2();
        if (D3DKMTEnumAdapters2(ref request) != 0 || request.NumAdapters == 0)
            return;

        int size = Marshal.SizeOf<AdapterInfo>();
        request.Adapters = Marshal.AllocHGlobal(size * (int)request.NumAdapters);
        try
        {
            if (D3DKMTEnumAdapters2(ref request) != 0)
                return;
            for (int i = 0; i < request.NumAdapters; i++)
                _adapters.Add(Marshal.PtrToStructure<AdapterInfo>(request.Adapters + i * size).Handle);
        }
        finally
        {
            Marshal.FreeHGlobal(request.Adapters);
        }
    }

    // hottest gpu in celsius, null if no driver reports a temperature
    public double? Read()
    {
        var temps = new List<double>();
        int size = Marshal.SizeOf<AdapterPerfData>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            foreach (var adapter in _adapters)
            {
                Marshal.StructureToPtr(new AdapterPerfData(), buffer, false);
                var query = new QueryAdapterInfo { Adapter = adapter, Type = KMTQAITYPE_ADAPTERPERFDATA, Data = buffer, DataSize = (uint)size };
                // software renderers (like microsoft basic render) just say no, skip them
                if (D3DKMTQueryAdapterInfo(ref query) == 0)
                    temps.Add(Marshal.PtrToStructure<AdapterPerfData>(buffer).Temperature / 10.0); // driver reports tenths of a degree
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return Hottest(temps);
    }

    // 0 means the driver doesn't report temperature, so it doesn't count
    public static double? Hottest(IEnumerable<double> temps)
    {
        var real = temps.Where(t => t > 0).ToList();
        return real.Count == 0 ? null : real.Max();
    }

    public void Dispose()
    {
        foreach (var adapter in _adapters)
        {
            var close = new CloseAdapter { Adapter = adapter };
            D3DKMTCloseAdapter(ref close);
        }
        _adapters.Clear();
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref EnumAdapters2 request);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo query);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter close);

    // layouts copied from the windows sdk (d3dkmthk.h)
    [StructLayout(LayoutKind.Sequential)]
    private struct EnumAdapters2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public uint Handle;
        public uint LuidLow;
        public int LuidHigh;
        public uint NumOfSources;
        public int PrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public IntPtr Data;
        public uint DataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter
    {
        public uint Adapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency, MaxMemoryFrequency, MaxMemoryFrequencyOC, MemoryBandwidth, PcieBandwidth;
        public uint FanRpm, Power, Temperature;
        public byte PowerStateOverride;
    }
}
