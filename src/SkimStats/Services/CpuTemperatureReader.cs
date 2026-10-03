using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace SkimStats.Services;

public enum CpuTempStatus { Off, Starting, Running, NeedsAdmin, NeedsDriver, NoSensor, Failed }

// cpu temperature via librehardwaremonitor, opt-in because it needs admin and the pawnio driver
// (the sensor lives inside the cpu and only kernel code can read it)
[SupportedOSPlatform("windows")]
public sealed class CpuTemperatureReader : IDisposable
{
    private readonly object _lock = new();
    private Computer? _computer;
    private IHardware? _cpu;
    private bool _wanted;

    public CpuTempStatus Status { get; private set; } = CpuTempStatus.Off;

    // raised on a background thread, listeners must hop to the ui thread themselves
    public event Action? StatusChanged;

    public static bool IsElevated =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public void Start()
    {
        if (_wanted)
            return;
        _wanted = true;

        if (!IsElevated)
        {
            SetStatus(CpuTempStatus.NeedsAdmin);
            return;
        }
        if (!PawnIo.IsInstalled)
        {
            SetStatus(CpuTempStatus.NeedsDriver);
            return;
        }

        // opening takes about half a second, keep it off the ui thread
        SetStatus(CpuTempStatus.Starting);
        Task.Run(() =>
        {
            try
            {
                var computer = new Computer { IsCpuEnabled = true }; // cpu only, the gpu part is slow and we don't need it
                computer.Open();
                var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                lock (_lock)
                {
                    // turned off again while we were opening
                    if (!_wanted)
                    {
                        computer.Close();
                        return;
                    }
                    _computer = computer;
                    _cpu = cpu;
                }
                SetStatus(cpu is null ? CpuTempStatus.NoSensor : CpuTempStatus.Running);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"couldn't open cpu sensors: {ex.Message}");
                SetStatus(CpuTempStatus.Failed);
            }
        });
    }

    public void Stop()
    {
        _wanted = false;
        lock (_lock)
        {
            _computer?.Close();
            _computer = null;
            _cpu = null;
        }
        SetStatus(CpuTempStatus.Off);
    }

    // celsius, null when not running or the sensor reads nothing
    public double? Read()
    {
        lock (_lock)
        {
            if (_cpu is null)
                return null;
            _cpu.Update(); // a few ms for the cpu alone
            return PickCpuTemperature(_cpu.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Select(s => (s.Name, s.Value)));
        }
    }

    // amd calls the main sensor "Core (Tctl/Tdie)", intel calls it "CPU Package",
    // otherwise fall back to the hottest one, 0 means the sensor isn't readable
    public static double? PickCpuTemperature(IEnumerable<(string Name, float? Value)> sensors)
    {
        var readable = sensors.Where(s => s.Value > 0).ToList();
        if (readable.Count == 0)
            return null;

        var main = readable.FirstOrDefault(s =>
            s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));
        return main.Name is not null ? main.Value : readable.Max(s => s.Value);
    }

    private void SetStatus(CpuTempStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        StatusChanged?.Invoke();
    }

    public void Dispose() => Stop();
}
