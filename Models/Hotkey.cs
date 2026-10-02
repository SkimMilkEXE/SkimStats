using System.Collections.Generic;
using Avalonia.Input;

namespace SkimStats.Models;

// a key combo in the form windows RegisterHotKey wants (MOD_ flags + virtual key code)
public record Hotkey(uint Modifiers, uint VirtualKey)
{
    public const uint Alt = 0x1;
    public const uint Control = 0x2;
    public const uint Shift = 0x4;

    public static Hotkey Default { get; } = new(Control | Shift, 'O');

    // turns an avalonia key press into a hotkey, null if it's not allowed
    // ponytail: only letters, digits, f-keys and numpad, add a full key map if people want others
    public static Hotkey? FromKeyPress(KeyModifiers modifiers, Key key)
    {
        uint? vk = key switch
        {
            >= Key.A and <= Key.Z => 'A' + (uint)(key - Key.A),
            >= Key.D0 and <= Key.D9 => '0' + (uint)(key - Key.D0),
            >= Key.NumPad0 and <= Key.NumPad9 => 0x60 + (uint)(key - Key.NumPad0),
            >= Key.F1 and <= Key.F24 => 0x70 + (uint)(key - Key.F1),
            _ => null,
        };
        if (vk is null)
            return null;

        uint mods = 0;
        if (modifiers.HasFlag(KeyModifiers.Alt)) mods |= Alt;
        if (modifiers.HasFlag(KeyModifiers.Control)) mods |= Control;
        if (modifiers.HasFlag(KeyModifiers.Shift)) mods |= Shift;

        // a bare letter (or shift+letter) would block normal typing everywhere, f-keys are fine alone
        bool isFKey = key is >= Key.F1 and <= Key.F24;
        if (!isFKey && (mods & (Control | Alt)) == 0)
            return null;

        return new Hotkey(mods, vk.Value);
    }

    // "Ctrl+Shift+O"
    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & Alt) != 0) parts.Add("Alt");
        if ((Modifiers & Shift) != 0) parts.Add("Shift");
        parts.Add(VirtualKey switch
        {
            >= 0x70 and <= 0x87 => $"F{VirtualKey - 0x6F}",
            >= 0x60 and <= 0x69 => $"Num{VirtualKey - 0x60}",
            _ => ((char)VirtualKey).ToString(),
        });
        return string.Join("+", parts);
    }
}
