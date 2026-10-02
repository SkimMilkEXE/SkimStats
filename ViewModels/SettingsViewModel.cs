using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkimStats.Models;
using SkimStats.Services;

namespace SkimStats.ViewModels;

[SupportedOSPlatform("windows")]
public partial class SettingsViewModel : ViewModelBase
{
    private readonly HotkeyService _hotkey;

    public AppSettings Settings { get; }
    public OverlayViewModel Overlay { get; }
    public IReadOnlyList<string> Monitors { get; }
    public OverlayCorner[] Corners { get; } = Enum.GetValues<OverlayCorner>();

    [ObservableProperty] public partial bool IsRecordingHotkey { get; set; }
    [ObservableProperty] public partial string HotkeyText { get; set; }
    [ObservableProperty] public partial string? HotkeyError { get; set; }
    [ObservableProperty] public partial string? StartupError { get; set; }

    public SettingsViewModel(AppSettings settings, OverlayViewModel overlay, HotkeyService hotkey, IReadOnlyList<string> monitors)
    {
        Settings = settings;
        Overlay = overlay;
        Monitors = monitors;
        _hotkey = hotkey;
        HotkeyText = HotkeyLabel();
    }

    // reads and writes the registry directly so it can't get out of sync
    public bool LaunchOnStartup
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            try
            {
                StartupRegistration.SetEnabled(value);
                StartupError = null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
            {
                Trace.WriteLine($"couldn't change startup setting: {ex.Message}");
                StartupError = "Couldn't change this setting, Windows blocked it.";
            }
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void StartRecordingHotkey()
    {
        // let go of the current combo so pressing it again gets recorded instead of toggling the overlay
        _hotkey.Unregister();
        IsRecordingHotkey = true;
        HotkeyError = null;
        HotkeyText = "Press a key combo… (Esc to cancel)";
    }

    // called by the view with the combo pressed, or null when cancelled
    public void FinishRecordingHotkey(Hotkey? pressed)
    {
        IsRecordingHotkey = false;

        if (pressed is not null && _hotkey.Register(pressed))
        {
            Settings.Hotkey = pressed;
        }
        else
        {
            if (pressed is not null)
                HotkeyError = $"{pressed} is already used by another app.";
            _hotkey.Register(Settings.Hotkey); // put the old one back
        }

        HotkeyText = HotkeyLabel();
    }

    private string HotkeyLabel() => _hotkey.Current?.ToString() ?? $"{Settings.Hotkey} (in use by another app)";
}
