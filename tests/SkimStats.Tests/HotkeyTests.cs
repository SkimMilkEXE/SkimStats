using Avalonia.Input;
using SkimStats.Models;

namespace SkimStats.Tests;

public class HotkeyTests
{
    [Fact]
    public void DefaultIsCtrlShiftO()
    {
        Assert.Equal("Ctrl+Shift+O", Hotkey.Default.ToString());
    }

    [Theory]
    [InlineData(Key.A, 0x41u)]
    [InlineData(Key.Z, 0x5Au)]
    [InlineData(Key.D0, 0x30u)]
    [InlineData(Key.D9, 0x39u)]
    [InlineData(Key.NumPad0, 0x60u)]
    [InlineData(Key.NumPad9, 0x69u)]
    [InlineData(Key.F1, 0x70u)]
    [InlineData(Key.F24, 0x87u)]
    public void MapsKeysToWindowsVirtualKeyCodes(Key key, uint expectedVirtualKey)
    {
        var hotkey = Hotkey.FromKeyPress(KeyModifiers.Control, key);

        Assert.NotNull(hotkey);
        Assert.Equal(expectedVirtualKey, hotkey.VirtualKey);
    }

    [Fact]
    public void MapsModifiersToWindowsFlags()
    {
        var hotkey = Hotkey.FromKeyPress(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, Key.K);

        Assert.Equal(new Hotkey(Hotkey.Control | Hotkey.Alt | Hotkey.Shift, 'K'), hotkey);
    }

    [Theory]
    [InlineData(KeyModifiers.None, Key.A)]   // would block typing "a" everywhere
    [InlineData(KeyModifiers.Shift, Key.A)]  // would block typing "A" everywhere
    [InlineData(KeyModifiers.Control, Key.Space)] // not a supported key
    [InlineData(KeyModifiers.Control, Key.LeftCtrl)] // just a modifier, still waiting for the real key
    public void RejectsCombosThatWouldBreakTypingOrArentSupported(KeyModifiers modifiers, Key key)
    {
        Assert.Null(Hotkey.FromKeyPress(modifiers, key));
    }

    [Fact]
    public void AllowsFKeysWithoutModifiers()
    {
        var hotkey = Hotkey.FromKeyPress(KeyModifiers.None, Key.F9);

        Assert.Equal(new Hotkey(0, 0x78), hotkey);
    }

    [Theory]
    [InlineData(Hotkey.Control | Hotkey.Alt, 'K', "Ctrl+Alt+K")]
    [InlineData(Hotkey.Shift | Hotkey.Alt, '7', "Alt+Shift+7")]
    [InlineData(0u, 0x7Bu, "F12")]
    [InlineData(Hotkey.Control, 0x65u, "Ctrl+Num5")]
    public void ShowsReadableLabel(uint modifiers, uint virtualKey, string expected)
    {
        Assert.Equal(expected, new Hotkey(modifiers, virtualKey).ToString());
    }

    [Fact]
    public void RecordedHotkeyRoundTripsToSameLabel()
    {
        var hotkey = Hotkey.FromKeyPress(KeyModifiers.Control | KeyModifiers.Shift, Key.F5);

        Assert.Equal("Ctrl+Shift+F5", hotkey?.ToString());
    }
}
