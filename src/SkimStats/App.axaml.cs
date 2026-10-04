using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using SkimStats.Models;
using SkimStats.Services;
using SkimStats.ViewModels;
using SkimStats.Views;

namespace SkimStats;

public partial class App : Application
{
    private AppSettings _settings = new();
    private StatsSampler? _sampler;
    private HotkeyService? _hotkey;
    private FpsMonitor? _fps;
    private CpuTemperatureReader? _cpuTemp;
    private readonly OverlayAutoShow _autoShow = new();
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlay;
    private OverlayViewModel? _overlayViewModel;
    private Window? _settingsWindow;
    private NativeMenuItem? _toggleMenuItem;
    private bool _quitting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;

            // after "restart as admin" the old copy is still closing, let it save settings
            // and release the hotkey before we load and register them
            WaitForPreviousInstance(desktop.Args);
            _settings = SettingsService.Load();

            MainViewModel mainViewModel;
            try
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("only windows is supported for now");

                // one sampler shared by every view
                _fps = new FpsMonitor();
                _cpuTemp = new CpuTemperatureReader();
                _sampler = new StatsSampler(new WindowsStatsProvider(_fps, _cpuTemp), TimeSpan.FromSeconds(1));
                mainViewModel = new MainViewModel(_sampler, _settings, ShowSettings);
                _sampler.Start();
            }
            catch (Exception ex)
            {
                mainViewModel = new MainViewModel($"Couldn't start stats: {ex.Message}");
            }

            _mainWindow = new MainWindow { DataContext = mainViewModel };

            // the lifetime shows MainWindow on startup, so leave it unset to start in the tray
            if (!_settings.StartMinimized)
                desktop.MainWindow = _mainWindow;

            // closing the window hides it to the tray, only quit from the tray really exits
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += (_, e) =>
            {
                if (_quitting)
                    return;
                e.Cancel = true;
                _mainWindow.Hide();
            };

            if (_sampler is not null)
            {
                _overlayViewModel = new OverlayViewModel(_sampler, _settings);
                _overlay = new OverlayWindow { DataContext = _overlayViewModel };
                _overlay.Show();
                // once a second is plenty, the stats tick already runs at that rate
                _sampler.SnapshotTaken += _ => UpdateOverlayVisibility();
            }

            if (OperatingSystem.IsWindows())
            {
                _hotkey = new HotkeyService(_mainWindow);
                _hotkey.Register(_settings.Hotkey);
                _hotkey.Pressed += ToggleOverlay;
            }

            CreateTrayIcon(desktop);
            _settings.PropertyChanged += OnSettingChanged;
            if (_settings.ShowFps && OperatingSystem.IsWindows())
                _fps?.Start();
            if (_settings.ShowCpuTemp && OperatingSystem.IsWindows())
                _cpuTemp?.Start();

            desktop.Exit += (_, _) =>
            {
                SettingsService.Save(_settings);
                if (OperatingSystem.IsWindows())
                {
                    _hotkey?.Dispose();
                    _fps?.Dispose();
                    _cpuTemp?.Dispose();
                }
                _sampler?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var show = new NativeMenuItem("Show SkimStats");
        show.Click += (_, _) => ShowMainWindow();

        _toggleMenuItem = new NativeMenuItem(ToggleMenuLabel()) { IsEnabled = _overlay is not null };
        _toggleMenuItem.Click += (_, _) => ToggleOverlay();

        var settings = new NativeMenuItem("Settings…") { IsEnabled = _overlay is not null && _hotkey is not null };
        settings.Click += (_, _) => ShowSettings();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
        {
            _quitting = true;
            desktop.Shutdown();
        };

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SkimStats/Assets/app-icon.ico"))),
            ToolTipText = "SkimStats",
            Menu = [show, _toggleMenuItem, settings, new NativeMenuItemSeparator(), quit],
        };
        tray.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, [tray]);
    }

    // tells the user up front if another app already took the hotkey
    private string ToggleMenuLabel() =>
        $"Toggle overlay ({(OperatingSystem.IsWindows() ? _hotkey?.Current?.ToString() : null) ?? "hotkey unavailable"})";

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Hotkey) && _toggleMenuItem is not null)
            _toggleMenuItem.Header = ToggleMenuLabel();

        if (e.PropertyName == nameof(AppSettings.OverlayMode))
            UpdateOverlayVisibility();

        // the cpu sensor driver only stays open while cpu temps are shown
        if (e.PropertyName == nameof(AppSettings.ShowCpuTemp) && OperatingSystem.IsWindows())
        {
            if (_settings.ShowCpuTemp)
                _cpuTemp?.Start();
            else
                _cpuTemp?.Stop();
        }

        // presentmon only runs while fps is shown
        if (e.PropertyName == nameof(AppSettings.ShowFps) && OperatingSystem.IsWindows())
        {
            if (_settings.ShowFps)
                _fps?.Start();
            else
                _fps?.Stop();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ShowSettings()
    {
        if (!OperatingSystem.IsWindows() || _overlay is null || _overlayViewModel is null || _hotkey is null || _fps is null || _cpuTemp is null)
            return;

        // only one settings window at a time
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // e.g. "1: 2560 x 1440 (main)"
        var monitors = _overlay.Screens.All
            .Select((s, i) => $"{i + 1}: {s.Bounds.Width} x {s.Bounds.Height}{(s.IsPrimary ? " (main)" : "")}")
            .ToList();

        _settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settings, _overlayViewModel, _hotkey, _fps, _cpuTemp, RestartAsAdmin, monitors),
        };
        _settingsWindow.Closed += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
                ((SettingsViewModel)_settingsWindow.DataContext!).Detach();
            _overlayViewModel.IsEditing = false; // lock the overlay again
            SettingsService.Save(_settings);
            _settingsWindow = null;
        };
        _settingsWindow.Show();
    }

    private void ToggleOverlay()
    {
        if (_overlay is null)
            return;
        _autoShow.Toggle(_settings.OverlayMode, ForegroundApp(), _overlay.IsVisible);
        UpdateOverlayVisibility();
    }

    private void UpdateOverlayVisibility()
    {
        if (_overlay is null || _overlayViewModel is null)
            return;
        bool show = _autoShow.ShouldShow(_settings.OverlayMode, ForegroundApp(), _settings.Games,
            _overlayViewModel.IsEditing, Environment.ProcessId);
        if (show && !_overlay.IsVisible)
            _overlay.Show();
        else if (!show && _overlay.IsVisible)
            _overlay.Hide();
    }

    private static ForegroundApp? ForegroundApp() =>
        OperatingSystem.IsWindows() ? Win32Interop.GetForegroundApp() : null;

    // starts an elevated copy (one uac prompt) and closes this one, false if the prompt was cancelled
    private bool RestartAsAdmin()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Environment.ProcessPath!, $"--wait-for-pid {Environment.ProcessId}")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // prompt cancelled or blocked
        }

        _quitting = true;
        _desktop?.Shutdown();
        return true;
    }

    private static void WaitForPreviousInstance(string[]? args)
    {
        if (args is null)
            return;
        int index = Array.IndexOf(args, "--wait-for-pid");
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out int pid))
            return;
        try
        {
            using var previous = System.Diagnostics.Process.GetProcessById(pid);
            previous.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            // already gone
        }
    }
}
