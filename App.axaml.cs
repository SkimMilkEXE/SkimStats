using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SkimStats.Services;
using SkimStats.ViewModels;
using SkimStats.Views;

namespace SkimStats;

public partial class App : Application
{
    private StatsSampler? _sampler;

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

            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
            desktop.Exit += (_, _) => _sampler?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}