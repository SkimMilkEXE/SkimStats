using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SkimStats.Services;

// raw win32 calls avalonia doesn't expose
[SupportedOSPlatform("windows")]
public static class Win32Interop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x20;     // mouse goes through to the window below
    private const long WS_EX_TOOLWINDOW = 0x80;      // hidden from alt-tab
    private const long WS_EX_LAYERED = 0x80000;      // needed for WS_EX_TRANSPARENT to pass clicks through
    private const long WS_EX_NOACTIVATE = 0x8000000; // never steals focus from the game

    // sets up the overlay window: layered, hidden from alt-tab, never focused, and click-through
    public static void MakeOverlay(IntPtr hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);

        // a layered window draws nothing until it gets an opacity, 255 = fully opaque
        // (the overlay's own transparency still comes from avalonia)
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    // edit mode turns this off so the overlay can be dragged, then back on to lock it
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style = clickThrough ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);
    }

    // 64-bit only, fine since we ship win-x64
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    // global hotkeys, windows sends WM_HOTKEY to hwnd when pressed anywhere
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000; // holding the keys down doesn't spam the event

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    // process id of the window the user is looking at (usually the game), 0 if none
    public static int ForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    // the app in front: its process, exe name, and whether it's a fullscreen (or borderless) window
    public static ForegroundApp? GetForegroundApp()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;
        GetWindowThreadProcessId(hwnd, out var pid);

        string exe;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            exe = process.ProcessName + ".exe";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            exe = ""; // closed in the meantime
        }

        return new ForegroundApp((int)pid, exe, IsFullscreenWindow(hwnd));
    }

    private static bool IsFullscreenWindow(IntPtr hwnd)
    {
        // the desktop is monitor sized too, but it's not a game
        if (hwnd == GetShellWindow() || ClassName(hwnd) is "Progman" or "WorkerW")
            return false;

        // games in fullscreen or borderless mode have no title bar, maximized apps do
        var style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
        if ((style & WS_CAPTION) == WS_CAPTION)
            return false;

        if (!GetWindowRect(hwnd, out var window))
            return false;
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info))
            return false;
        return CoversMonitor(window, info.Monitor);
    }

    public static bool CoversMonitor(Rect window, Rect monitor) =>
        window.Left <= monitor.Left && window.Top <= monitor.Top &&
        window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

    private static string ClassName(IntPtr hwnd)
    {
        var name = new System.Text.StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        return name.ToString();
    }

    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0xC00000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public Rect(int left, int top, int right, int bottom) => (Left, Top, Right, Bottom) = (left, top, right, bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    // a job kills every process inside it once its handle closes, which windows does for us
    // when skimstats exits or crashes, so helper processes can never outlive the app
    public static IntPtr CreateKillOnCloseJob()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        var info = new JobExtendedLimitInformation();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JobExtendedLimitInformation>());
        return job;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobExtendedLimitInformation info, uint length);

    // layouts copied from the windows sdk, only LimitFlags is actually used
    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedLimitInformation
    {
        public JobBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
