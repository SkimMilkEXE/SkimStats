using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SkimStats.Services;

public enum OverlayMode { Always, Fullscreen, GameList }

// the app in front right now, filled in from win32 once per tick
public record ForegroundApp(int ProcessId, string ExeName, bool IsFullscreen);

// decides whether the overlay should be on screen, checked once per stats tick
public sealed class OverlayAutoShow
{
    // always mode: the hotkey hides it until pressed again
    private bool _hiddenInAlwaysMode;

    // auto modes: the hotkey flips it just for the app in front, until another app comes to the front
    private int? _overrideForProcess;
    private bool _overrideShow;

    public bool ShouldShow(OverlayMode mode, ForegroundApp? foreground, IEnumerable<string> games, bool editing, int ownProcessId)
    {
        // can't drag or preview what you can't see
        if (editing)
            return true;
        if (mode == OverlayMode.Always)
            return !_hiddenInAlwaysMode;
        // our own settings window is in front, keep it visible so changes can be previewed
        if (foreground?.ProcessId == ownProcessId)
            return true;

        if (_overrideForProcess is { } pid)
        {
            if (foreground?.ProcessId == pid)
                return _overrideShow;
            _overrideForProcess = null; // switched apps, back to automatic
        }
        return Matches(mode, foreground, games);
    }

    public void Toggle(OverlayMode mode, ForegroundApp? foreground, bool currentlyShown)
    {
        if (mode == OverlayMode.Always)
        {
            _hiddenInAlwaysMode = !_hiddenInAlwaysMode;
        }
        else if (foreground is not null)
        {
            _overrideForProcess = foreground.ProcessId;
            _overrideShow = !currentlyShown;
        }
    }

    // fullscreen but not games: browsers playing video, video players, slideshows
    // ponytail: fixed list, the game list mode covers anything this misses
    private static readonly HashSet<string> NotGames =
    [
        "chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "brave.exe", "vivaldi.exe", "arc.exe", "iexplore.exe",
        "vlc.exe", "mpv.exe", "mpc-hc64.exe", "mpc-be64.exe", "potplayermini64.exe", "wmplayer.exe",
        "microsoft.media.player.exe", "video.ui.exe", "applicationframehost.exe", // windows media player, store apps like netflix
        "powerpnt.exe", "discord.exe", "spotify.exe",
    ];

    public static bool Matches(OverlayMode mode, ForegroundApp? foreground, IEnumerable<string> games) =>
        foreground is not null && mode switch
        {
            OverlayMode.Fullscreen => foreground.IsFullscreen && !NotGames.Contains(NormalizeExe(foreground.ExeName)),
            OverlayMode.GameList => games.Any(g => SameExe(g, foreground.ExeName)),
            _ => true,
        };

    // "C:\Games\EldenRing.exe", "eldenring.exe" and "eldenring" are all the same game
    public static string NormalizeExe(string pathOrName)
    {
        var name = Path.GetFileName(pathOrName.Trim()).ToLowerInvariant();
        return name.EndsWith(".exe", StringComparison.Ordinal) ? name : name + ".exe";
    }

    private static bool SameExe(string a, string b) => NormalizeExe(a) == NormalizeExe(b);
}
