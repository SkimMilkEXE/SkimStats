using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkimStats.Models;
using SkimStats.ViewModels;

namespace SkimStats.Views;

[SupportedOSPlatform("windows")]
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        // tunnel so we see keys before the focused button does
        AddHandler(KeyDownEvent, OnKeyDownWhileRecording, RoutingStrategies.Tunnel);

        // fresh list of running apps every time the dropdown opens
        RunningAppsBox.DropDownOpened += (_, _) => (DataContext as SettingsViewModel)?.RefreshRunningApps();
    }

    private async void OnBrowseGame(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a game's .exe",
            FileTypeFilter = [new FilePickerFileType("Programs") { Patterns = ["*.exe"] }],
        });
        if (files.Count > 0 && DataContext is SettingsViewModel vm)
            vm.AddGame(files[0].Name);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        // closing mid-recording would otherwise leave no hotkey registered
        if (DataContext is SettingsViewModel { IsRecordingHotkey: true } vm)
            vm.FinishRecordingHotkey(null);
    }

    private void OnKeyDownWhileRecording(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel { IsRecordingHotkey: true } vm)
            return;

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            vm.FinishRecordingHotkey(null);
            return;
        }

        // modifier-only presses return null, keep waiting for the real key
        if (Hotkey.FromKeyPress(e.KeyModifiers, e.Key) is { } hotkey)
            vm.FinishRecordingHotkey(hotkey);
    }
}
