namespace SkimStats.ViewModels;

// turns raw numbers into short display text
public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    // 1536 -> "1.5 KB", uses 1024 steps like windows does
    public static string Bytes(double bytes)
    {
        int unit = 0;
        while (bytes >= 1024 && unit < Units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes:0} B" : $"{bytes:0.0} {Units[unit]}";
    }

    public static string Rate(double bytesPerSec) => Bytes(bytesPerSec) + "/s";

    // "FPS 144 · 1% 97 · 6.9 ms", parts after fps are optional, "FPS --" when nothing is drawing
    public static string FpsLine(SkimStats.Services.FrameStats? frames, bool showLow, bool showFrameTime)
    {
        if (frames is null)
            return "FPS --";
        var text = $"FPS {frames.Fps:0}";
        if (showLow)
            text += $" · 1% {frames.OnePercentLowFps:0}";
        if (showFrameTime)
            text += $" · {frames.FrameTimeMs:0.0} ms";
        return text;
    }

    // "21%" or "21% · 62°C", usage of null shows "--"
    public static string UsageWithTemp(double? percent, double? tempC, bool showTemp)
    {
        var usage = percent is { } p ? $"{p:0}%" : "--";
        return showTemp && tempC is { } t ? $"{usage} · {t:0}°C" : usage;
    }
}
