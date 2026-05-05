using Clicky.Capture.Hotkeys;
using Clicky.Capture.ScreenCapture;
using Clicky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Clicky.App;

public static class ServiceRegistration
{
    /// <summary>Registers all Clicky application services.</summary>
    public static IServiceCollection AddClickyServices(this IServiceCollection services)
    {
        services.AddSingleton<IPushToTalkHook, PushToTalkHook>();
        services.AddSingleton<IScreenCaptureService, WgcCaptureService>();
        return services;
    }
}
