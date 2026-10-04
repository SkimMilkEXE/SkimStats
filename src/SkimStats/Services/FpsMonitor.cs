using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SkimStats.Services;

public enum FpsStatus { Off, Running, NeedsPermission, Missing, Failed }

// what the overlay shows for the game in front
// fps and frame time cover the last second, the 1% low covers the last 10 seconds,
// worst frame time is the slowest frame of the last second (spikes = stutter)
public record FrameStats(double Fps, double OnePercentLowFps, double FrameTimeMs, double WorstFrameTimeMs);

// fps for the window in front, using intel presentmon as a helper process
// presentmon reads the frame events windows already logs (etw), nothing touches the game itself
[SupportedOSPlatform("windows")]
public sealed class FpsMonitor : IDisposable
{
    private const string SessionName = "SkimStats";
    private const double WindowMs = 1000;        // average fps over the last second of frames
    private const double LowWindowMs = 10000;    // 1% low needs more frames, 1% of one second is barely one frame
    private const long StaleAfterMs = 2000;      // no frames for this long = app isn't drawing (minimized, paused)
    private const string PerformanceLogUsersSid = "S-1-5-32-559";

    private static readonly string ExePath = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");

    // frame times per process, filled from presentmon's output thread
    private readonly object _lock = new();
    private readonly Dictionary<int, ProcessFrames> _frames = [];

    private IntPtr _job;
    private Process? _presentMon;
    private bool _stopping;
    private string? _lastError;
    private int _pidColumn = -1, _timeColumn = -1, _frameTimeColumn = -1;

    public FpsStatus Status { get; private set; } = FpsStatus.Off;

    // raised on a background thread, listeners must hop to the ui thread themselves
    public event Action? StatusChanged;

    // admins can always read the events, everyone else needs the performance log users group
    // (group changes only show up in the token after signing out and back in)
    public static bool HasPermission
    {
        get
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator)
                || principal.IsInRole(new SecurityIdentifier(PerformanceLogUsersSid));
        }
    }

    public void Start()
    {
        if (Status == FpsStatus.Running)
            return;

        if (!File.Exists(ExePath))
        {
            SetStatus(FpsStatus.Missing);
            return;
        }
        if (!HasPermission)
        {
            SetStatus(FpsStatus.NeedsPermission);
            return;
        }

        // a previous run that crashed leaves its process object behind
        _presentMon?.Dispose();
        _presentMon = null;

        _stopping = false;
        _lastError = null;
        _pidColumn = _timeColumn = _frameTimeColumn = -1;

        // csv to stdout, skip the display/input/gpu tracking we don't use to keep it light
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(ExePath,
                $"--output_stdout --no_console_stats --session_name {SessionName} --stop_existing_session " +
                "--no_track_display --no_track_input --no_track_gpu")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) OnLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) _lastError = e.Data.Trim(); };
        process.Exited += (_, _) => OnExited();

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            Trace.WriteLine($"couldn't start presentmon: {ex.Message}");
            SetStatus(FpsStatus.Failed);
            return;
        }

        // tie presentmon's life to ours so it can't keep running if we crash
        if (_job == IntPtr.Zero)
            _job = Win32Interop.CreateKillOnCloseJob();
        Win32Interop.AssignProcessToJobObject(_job, process.Handle);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _presentMon = process;
        SetStatus(FpsStatus.Running);
    }

    public void Stop()
    {
        if (_presentMon is null)
        {
            SetStatus(FpsStatus.Off);
            return;
        }

        _stopping = true;
        try
        {
            _presentMon.Kill();
            _presentMon.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
            // already exited
        }
        _presentMon.Dispose();
        _presentMon = null;

        // killing presentmon leaves its trace session behind, this asks windows to close it
        try
        {
            using var cleanup = Process.Start(new ProcessStartInfo(ExePath,
                $"--terminate_existing_session --session_name {SessionName}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            cleanup?.WaitForExit(3000);
        }
        catch (Win32Exception ex)
        {
            Trace.WriteLine($"couldn't close presentmon session: {ex.Message}");
        }

        lock (_lock)
            _frames.Clear();
        SetStatus(FpsStatus.Off);
    }

    // null when the foreground app isn't drawing frames (or is skimstats itself)
    public FrameStats? ForegroundFrameStats()
    {
        int pid = Win32Interop.ForegroundProcessId();
        return pid == 0 || pid == Environment.ProcessId ? null : FrameStatsFor(pid);
    }

    public FrameStats? FrameStatsFor(int pid)
    {
        long now = Environment.TickCount64;
        lock (_lock)
        {
            // drop processes that stopped drawing so the dictionary doesn't grow forever
            foreach (var stale in _frames.Where(f => now - f.Value.LastSeen > StaleAfterMs).Select(f => f.Key).ToList())
                _frames.Remove(stale);

            return _frames.TryGetValue(pid, out var frames) && frames.Times.Count > 0
                ? ComputeStats(frames.Times.ToList())
                : null;
        }
    }

    // frames are (timestamp, frame time) oldest first, covering up to the last 10 seconds
    public static FrameStats ComputeStats(IReadOnlyList<(double TimeMs, double FrameMs)> frames)
    {
        double newest = frames[^1].TimeMs;
        var lastSecond = frames.Where(f => f.TimeMs >= newest - WindowMs).Select(f => f.FrameMs).ToList();
        return new FrameStats(
            AverageFps(lastSecond),
            OnePercentLowFps(frames.Select(f => f.FrameMs).ToList()),
            lastSecond.Average(),
            lastSecond.Max());
    }

    // average fps of the slowest 1% of frames (at least one frame)
    // e.g. 990 frames at 7ms and 10 at 20ms -> the 10 slow ones decide it -> 50 fps
    // (a plain 99th percentile would land on a 7ms frame here and hide the stutter)
    public static double OnePercentLowFps(IReadOnlyCollection<double> frameTimesMs)
    {
        if (frameTimesMs.Count == 0)
            return 0;
        int count = Math.Max(1, (int)Math.Ceiling(frameTimesMs.Count * 0.01));
        return AverageFps(frameTimesMs.OrderDescending().Take(count).ToList());
    }

    // average fps from frame times, e.g. sixty frames of 16.67ms -> 60
    public static double AverageFps(IReadOnlyCollection<double> frameTimesMs)
    {
        double total = frameTimesMs.Sum();
        return total <= 0 ? 0 : 1000.0 * frameTimesMs.Count / total;
    }

    // adds this windows account to performance log users, shows one uac prompt
    // returns null on success, otherwise a message for the user
    public static async Task<string?> GrantPermissionAsync()
    {
        var userSid = WindowsIdentity.GetCurrent().User?.Value;
        if (userSid is null)
            return "Couldn't find your Windows account.";

        // already being a member counts as success
        var command =
            $"try {{ Add-LocalGroupMember -SID {PerformanceLogUsersSid} -Member {userSid} -ErrorAction Stop }} " +
            "catch [Microsoft.PowerShell.Commands.MemberExistsException] { }";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{command}\"")
            {
                UseShellExecute = true,
                Verb = "runas", // asks windows for admin, this is the uac prompt
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null)
                return "Couldn't start the permission helper.";
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? null : "Windows couldn't add the permission.";
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return "Permission prompt was cancelled.";
        }
        catch (Win32Exception ex)
        {
            return $"Couldn't change the permission: {ex.Message}";
        }
    }

    private void OnLine(string line)
    {
        var columns = line.Split(',');

        // first line is the header, find our columns by name so new presentmon versions don't break us
        if (_pidColumn < 0)
        {
            _pidColumn = Array.IndexOf(columns, "ProcessID");
            _timeColumn = Array.IndexOf(columns, "TimeInMs");
            _frameTimeColumn = Array.IndexOf(columns, "MsBetweenPresents");
            if (_timeColumn < 0 || _frameTimeColumn < 0)
                _pidColumn = -1; // keep looking, presentmon sometimes prints notes before the header
            return;
        }

        int needed = Math.Max(_pidColumn, Math.Max(_timeColumn, _frameTimeColumn));
        if (columns.Length <= needed
            || !int.TryParse(columns[_pidColumn], out int pid)
            || !double.TryParse(columns[_timeColumn], System.Globalization.CultureInfo.InvariantCulture, out double timeMs)
            || !double.TryParse(columns[_frameTimeColumn], System.Globalization.CultureInfo.InvariantCulture, out double frameMs))
            return; // first frame of each app has "NA" frame time

        lock (_lock)
        {
            if (!_frames.TryGetValue(pid, out var frames))
                _frames[pid] = frames = new ProcessFrames();

            frames.Times.Enqueue((timeMs, frameMs));
            frames.LastSeen = Environment.TickCount64;
            while (frames.Times.Peek().TimeMs < timeMs - LowWindowMs)
                frames.Times.Dequeue();
        }
    }

    private void OnExited()
    {
        if (_stopping)
            return;

        Trace.WriteLine($"presentmon exited: {_lastError}");
        // presentmon says "access denied" when the account can't read the events
        bool denied = _lastError?.Contains("denied", StringComparison.OrdinalIgnoreCase) == true;
        SetStatus(denied ? FpsStatus.NeedsPermission : FpsStatus.Failed);
    }

    private void SetStatus(FpsStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        StatusChanged?.Invoke();
    }

    public void Dispose() => Stop();

    private sealed class ProcessFrames
    {
        public Queue<(double TimeMs, double FrameMs)> Times { get; } = new();
        public long LastSeen { get; set; }
    }
}
