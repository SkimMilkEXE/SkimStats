using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace SkimStats.Services;

// gpu usage % the way task manager shows it, from windows' built-in "GPU Engine" counters
// works on any gpu brand, no admin needed
[SupportedOSPlatform("windows")]
public sealed class GpuUsageReader
{
    private readonly PerformanceCounterCategory _category = new("GPU Engine");
    private readonly Task _warmup;
    private Dictionary<string, CounterSample> _last = [];

    public GpuUsageReader()
    {
        // the very first read takes about a second, do it off the ui thread
        // if this pc has no gpu counters (old drivers, some vms) the task fails and gpu stays null
        _warmup = Task.Run(() => _last = ReadSamples());
    }

    // null while warming up or when gpu counters aren't available
    public double? Read()
    {
        if (!_warmup.IsCompletedSuccessfully)
            return null;

        Dictionary<string, CounterSample> now;
        try
        {
            now = ReadSamples();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Trace.WriteLine($"gpu read failed: {ex.Message}");
            return null;
        }

        // utilization is a rate, so each value needs this sample and the one from last second
        var perProcessEngines = now
            .Where(s => _last.ContainsKey(s.Key))
            .Select(s => (s.Key, (double)CounterSample.Calculate(_last[s.Key], s.Value)));
        var usage = BusiestEngine(perProcessEngines);

        _last = now;
        return usage;
    }

    // one counter per process per engine, e.g. "pid_123_luid_0x0_0x5a24_phys_0_eng_3_engtype_3d"
    private Dictionary<string, CounterSample> ReadSamples()
    {
        var data = _category.ReadCategory()["utilization percentage"];
        var samples = new Dictionary<string, CounterSample>(data.Count);
        foreach (InstanceData instance in data.Values)
            samples[instance.InstanceName] = instance.Sample;
        return samples;
    }

    // add up every process per engine (3d, copy, video decode...), the busiest engine is the gpu %
    // checks every gpu in the pc, so a laptop's igpu + dgpu reports whichever is working hardest
    public static double BusiestEngine(IEnumerable<(string Instance, double Percent)> perProcessEngines)
    {
        var perEngine = new Dictionary<string, double>();
        foreach (var (instance, percent) in perProcessEngines)
        {
            // drop the "pid_123_" part so every process on the same engine adds up together
            int start = instance.IndexOf("luid_", StringComparison.Ordinal);
            var engine = start >= 0 ? instance[start..] : instance;
            perEngine[engine] = perEngine.GetValueOrDefault(engine) + percent;
        }
        return perEngine.Count == 0 ? 0 : Math.Clamp(perEngine.Values.Max(), 0, 100);
    }
}
