using Clicky.Core;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;
using System.Windows;

namespace Clicky.App;

public partial class App : Application
{
    private IServiceProvider? _services;
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Clicky", "logs", "clicky-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        base.OnStartup(e);

        try
        {
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            Log.Information("Tray icon initialized");

            var services = new ServiceCollection();
            services.AddClickyServices();
            _services = services.BuildServiceProvider();

            var ptt = _services.GetRequiredService<IPushToTalkHook>();
            ptt.RecordingStarted += (_, _) => Log.Information("Recording started");
            ptt.Start();

            var companion = _services.GetRequiredService<ICompanionOrchestrator>();
            companion.Start();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start Clicky");
            Log.CloseAndFlush();
            Shutdown(1);
        }
    }

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Log.Information("Settings clicked (not yet implemented)");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        Log.CloseAndFlush();
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
