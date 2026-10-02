using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using SkimStats.Services;
using SkimStats.ViewModels;
using SkimStats.Views;

namespace SkimStats;

public partial class App : Application
{
    private const uint VK_O = 0x4F;

    private StatsSampler? _sampler;
    private HotkeyService? _hotkey;
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlay;
    private bool _quitting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel mainViewModel;
            try
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("only windows is supported for now");

                // one sampler shared by every view
                _sampler = new StatsSampler(new WindowsStatsProvider(), TimeSpan.FromSeconds(1));
                mainViewModel = new MainViewModel(_sampler);
                _sampler.Start();
            }
            catch (Exception ex)
            {
                mainViewModel = new MainViewModel($"Couldn't start stats: {ex.Message}");
            }

            _mainWindow = new MainWindow { DataContext = mainViewModel };
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
                _overlay = new OverlayWindow { DataContext = new OverlayViewModel(_sampler) };
                _overlay.Show();
            }

            // ctrl+shift+o for now, becomes a setting later
            if (OperatingSystem.IsWindows())
            {
                _hotkey = new HotkeyService(_mainWindow, Win32Interop.MOD_CONTROL | Win32Interop.MOD_SHIFT, VK_O);
                _hotkey.Pressed += ToggleOverlay;
            }

            CreateTrayIcon(desktop);

            desktop.Exit += (_, _) =>
            {
                _hotkey?.Dispose();
                _sampler?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // tells the user up front if another app already took the hotkey
        var hotkeyLabel = _hotkey?.IsRegistered == true ? "Ctrl+Shift+O" : "hotkey unavailable";

        var show = new NativeMenuItem("Show SkimStats");
        show.Click += (_, _) => ShowMainWindow();

        var toggle = new NativeMenuItem($"Toggle overlay ({hotkeyLabel})") { IsEnabled = _overlay is not null };
        toggle.Click += (_, _) => ToggleOverlay();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
        {
            _quitting = true;
            desktop.Shutdown();
        };

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SkimStats/Assets/avalonia-logo.ico"))),
            ToolTipText = "SkimStats",
            Menu = [show, toggle, new NativeMenuItemSeparator(), quit],
        };
        tray.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, [tray]);
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

    private void ToggleOverlay()
    {
        if (_overlay is null)
            return;
        if (_overlay.IsVisible)
            _overlay.Hide();
        else
            _overlay.Show();
    }
}
