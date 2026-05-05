namespace Clicky.Core;

/// <summary>Runtime configuration for the companion.</summary>
public sealed class CompanionSettings
{
    /// <summary>HTTP endpoint for the AI worker that processes screenshots and audio.</summary>
    public string WorkerUrl { get; init; } = "https://httpbin.org/post";

    /// <summary>AssemblyAI API key used to authenticate the streaming WebSocket connection.</summary>
    public string AssemblyAiApiKey { get; init; } = string.Empty;
}
