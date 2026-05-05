using Clicky.Capture.Hotkeys;
using Clicky.Capture.ScreenCapture;
using Clicky.Core;
using Clicky.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Clicky.App;

public static class ServiceRegistration
{
    /// <summary>Registers all Clicky application services.</summary>
    public static IServiceCollection AddClickyServices(this IServiceCollection services)
    {
        services.AddSingleton<IPushToTalkHook, PushToTalkHook>();
        services.AddSingleton<IScreenCaptureService, WgcCaptureService>();

        // Read WorkerUrl from environment (falls back to httpbin for dev)
        var workerUrl = Environment.GetEnvironmentVariable("WORKER_URL") ?? "https://httpbin.org/post";
        services.AddSingleton(new CompanionSettings { WorkerUrl = workerUrl });
        services.AddHttpClient<ICompanionOrchestrator, CompanionOrchestrator>();

        return services;
    }
}
