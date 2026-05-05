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

        // Read settings from environment; fall back to safe defaults for dev
        var workerUrl = Environment.GetEnvironmentVariable("WORKER_URL") ?? "https://httpbin.org/post";
        var assemblyAiApiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY") ?? string.Empty;
        services.AddSingleton(new CompanionSettings
        {
            WorkerUrl = workerUrl,
            AssemblyAiApiKey = assemblyAiApiKey,
        });
        services.AddHttpClient<ICompanionOrchestrator, CompanionOrchestrator>();

        return services;
    }
}
