using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;
using System.Windows;

namespace Clicky.App;

public partial class App : Application
{
    private IServiceProvider? _services;

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
            var services = new ServiceCollection();
            services.AddClickyServices();
            _services = services.BuildServiceProvider();

            // Start services (Task 2 adds tray icon host here)
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start Clicky");
            Log.CloseAndFlush();
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
