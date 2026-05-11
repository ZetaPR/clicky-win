using Clicky.Core;
using Clicky.Core.Settings;
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

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled domain exception terminating={IsTerminating}", args.IsTerminating);
            Log.CloseAndFlush();
        };

        base.OnStartup(e);

        try
        {
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            _trayIcon.ForceCreate();
            Log.Information("Tray icon initialized");

            var services = new ServiceCollection();
            services.AddClickyServices();
            _services = services.BuildServiceProvider();
            Log.Information("DI container built");

            (_services.GetRequiredService<IOverlayService>() as Window)?.Show();
            Log.Information("Overlay shown");

            var ptt = _services.GetRequiredService<IPushToTalkHook>();
            ptt.RecordingStarted += (_, _) => Log.Information("Recording started");
            Log.Information("Starting PTT hook");
            ptt.Start();
            Log.Information("PTT hook started");

            var companion = _services.GetRequiredService<ICompanionOrchestrator>();
            companion.Start();
            Log.Information("Companion started — ready");
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
        var svc      = _services!.GetRequiredService<IUserSettingsService>();
        var overlay  = _services!.GetRequiredService<IOverlayService>();
        var companion = _services!.GetRequiredService<CompanionSettings>();
        new SettingsWindow(svc.Load(), svc, overlay, companion).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cancel in-flight companion operations first, then release input hook
        _services?.GetService<ICompanionOrchestrator>()?.Dispose();
        _services?.GetService<IPushToTalkHook>()?.Dispose();
        _trayIcon?.Dispose();
        Log.CloseAndFlush();
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
