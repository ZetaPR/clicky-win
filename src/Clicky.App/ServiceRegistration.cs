using Clicky.Capture.Audio;
using Clicky.Capture.Hotkeys;
using Clicky.Capture.ScreenCapture;
using Clicky.Core;
using Clicky.Overlay;
using Clicky.Services;
using Clicky.Services.Audio;
using Microsoft.Extensions.DependencyInjection;

namespace Clicky.App;

public static class ServiceRegistration
{
    /// <summary>Registers all Clicky application services.</summary>
    public static IServiceCollection AddClickyServices(this IServiceCollection services)
    {
        services.AddSingleton<IPushToTalkHook, PushToTalkHook>();
        services.AddSingleton<IScreenCaptureService, WgcCaptureService>();
        services.AddSingleton<IMicrophoneRecorder, WasapiMicrophoneRecorder>();
        services.AddSingleton<ITranscriptionService, AssemblyAITranscriptionService>();
        services.AddSingleton<IOverlayService, CursorOverlayWindow>();
        services.AddSingleton<StepPlanStore>();
        services.AddSingleton<StepClickWatcher>();
        services.AddHttpClient<IStepVerifier, CloudflareWorkerVerifyService>();
        services.AddSingleton<ICompanionOrchestrator, CompanionOrchestrator>();
        services.AddHttpClient<ILlmService, CloudflareWorkerLlmService>();
        services.AddHttpClient<ITtsService, CartesiaTtsService>();

        // Read settings from environment; fall back to safe defaults for dev
        var workerUrl = Environment.GetEnvironmentVariable("WORKER_URL") ?? "https://httpbin.org/post";
        var assemblyAiApiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY") ?? string.Empty;
        var cartesiaApiKey = Environment.GetEnvironmentVariable("CARTESIA_API_KEY") ?? string.Empty;
        var cartesiaVoiceId = Environment.GetEnvironmentVariable("CARTESIA_VOICE_ID") ?? string.Empty;
        services.AddSingleton(new CompanionSettings
        {
            WorkerUrl = workerUrl,
            AssemblyAiApiKey = assemblyAiApiKey,
            CartesiaApiKey = cartesiaApiKey,
            CartesiaVoiceId = string.IsNullOrEmpty(cartesiaVoiceId)
                ? "a0e99841-438c-4a64-b679-ae501e7d6091"
                : cartesiaVoiceId,
        });

        return services;
    }
}
