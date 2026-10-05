using System.Runtime.Versioning;
using SkimStats.Services;

namespace SkimStats.Tests;

public class OverlayAutoShowTests
{
    private const int Own = 1;
    private static readonly ForegroundApp Game = new(100, "eldenring.exe", IsFullscreen: true);
    private static readonly ForegroundApp Browser = new(200, "opera.exe", IsFullscreen: false);
    private static readonly string[] MyGames = ["EldenRing.exe"];

    [Fact]
    public void AlwaysModeShowsForEverything()
    {
        var auto = new OverlayAutoShow();

        Assert.True(auto.ShouldShow(OverlayMode.Always, Browser, MyGames, editing: false, Own));
        Assert.True(auto.ShouldShow(OverlayMode.Always, null, MyGames, editing: false, Own));
    }

    [Fact]
    public void FullscreenModeFollowsTheAppInFront()
    {
        var auto = new OverlayAutoShow();

        Assert.True(auto.ShouldShow(OverlayMode.Fullscreen, Game, [], editing: false, Own));
        Assert.False(auto.ShouldShow(OverlayMode.Fullscreen, Browser, [], editing: false, Own));
    }

    [Fact]
    public void FullscreenModeSkipsBrowsersPlayingVideo()
    {
        var auto = new OverlayAutoShow();
        var youtube = new ForegroundApp(300, "Chrome.exe", IsFullscreen: true);

        Assert.False(auto.ShouldShow(OverlayMode.Fullscreen, youtube, [], editing: false, Own));
    }

    [Fact]
    public void GameListMatchesExeNamesIgnoringCase()
    {
        var auto = new OverlayAutoShow();

        Assert.True(auto.ShouldShow(OverlayMode.GameList, Game, MyGames, editing: false, Own));
        Assert.False(auto.ShouldShow(OverlayMode.GameList, Browser, MyGames, editing: false, Own));
    }

    [Fact]
    public void EditingAndOwnWindowsAlwaysShow()
    {
        var auto = new OverlayAutoShow();
        var settingsWindow = new ForegroundApp(Own, "skimstats.exe", false);

        Assert.True(auto.ShouldShow(OverlayMode.GameList, Browser, MyGames, editing: true, Own));
        Assert.True(auto.ShouldShow(OverlayMode.GameList, settingsWindow, MyGames, editing: false, Own));
    }

    [Fact]
    public void HotkeyInAutoModeHidesUntilTheAppChanges()
    {
        var auto = new OverlayAutoShow();
        auto.Toggle(OverlayMode.Fullscreen, Game, currentlyShown: true);

        Assert.False(auto.ShouldShow(OverlayMode.Fullscreen, Game, [], editing: false, Own));
        // alt-tab to the browser clears the override
        Assert.False(auto.ShouldShow(OverlayMode.Fullscreen, Browser, [], editing: false, Own));
        // back in the game it's automatic again, so it shows
        Assert.True(auto.ShouldShow(OverlayMode.Fullscreen, Game, [], editing: false, Own));
    }

    [Fact]
    public void HotkeyCanForceShowForAnAppThatWouldBeHidden()
    {
        var auto = new OverlayAutoShow();
        auto.Toggle(OverlayMode.Fullscreen, Browser, currentlyShown: false);

        Assert.True(auto.ShouldShow(OverlayMode.Fullscreen, Browser, [], editing: false, Own));
    }

    [Fact]
    public void HotkeyInAlwaysModeStaysHiddenUntilPressedAgain()
    {
        var auto = new OverlayAutoShow();
        auto.Toggle(OverlayMode.Always, Game, currentlyShown: true);

        Assert.False(auto.ShouldShow(OverlayMode.Always, Browser, [], editing: false, Own));
        auto.Toggle(OverlayMode.Always, Browser, currentlyShown: false);
        Assert.True(auto.ShouldShow(OverlayMode.Always, Browser, [], editing: false, Own));
    }

    [Theory]
    [InlineData(@"C:\Games\Elden Ring\EldenRing.exe", "eldenring.exe")]
    [InlineData("eldenring", "eldenring.exe")]
    [InlineData("  Cs2.EXE ", "cs2.exe")]
    public void NormalizesPathsAndNames(string input, string expected)
    {
        Assert.Equal(expected, OverlayAutoShow.NormalizeExe(input));
    }

    [SupportedOSPlatform("windows")]
    [Theory]
    [InlineData(0, 0, 2560, 1440, true)]        // exactly the monitor
    [InlineData(-8, -8, 2568, 1448, true)]      // slightly bigger, some games do this
    [InlineData(0, 0, 2560, 1392, false)]       // leaves the taskbar visible
    [InlineData(100, 100, 1380, 820, false)]    // a normal window
    public void CoversMonitorChecksTheWholeScreen(int left, int top, int right, int bottom, bool expected)
    {
        var monitor = new Win32Interop.Rect(0, 0, 2560, 1440);

        Assert.Equal(expected, Win32Interop.CoversMonitor(new Win32Interop.Rect(left, top, right, bottom), monitor));
    }
}
