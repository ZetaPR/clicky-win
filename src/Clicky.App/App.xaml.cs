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
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Clicky", "logs", "clicky-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        base.OnStartup(e);

        var services = new ServiceCollection();
        ServiceRegistration.Register(services);
        _services = services.BuildServiceProvider();

        // Start services (Task 2 adds tray icon host here)
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
