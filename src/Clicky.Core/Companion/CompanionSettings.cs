namespace Clicky.Core;

/// <summary>Runtime configuration for the companion.</summary>
public sealed class CompanionSettings
{
    /// <summary>HTTP endpoint for the AI worker that processes screenshots and audio.</summary>
    public string WorkerUrl { get; init; } = "https://httpbin.org/post";

    /// <summary>AssemblyAI API key used to authenticate the streaming WebSocket connection.</summary>
    public string AssemblyAiApiKey { get; init; } = string.Empty;

    /// <summary>Cartesia API key used to authenticate TTS requests.</summary>
    public string CartesiaApiKey { get; init; } = string.Empty;

    /// <summary>Cartesia voice ID to use for TTS synthesis.</summary>
    public string CartesiaVoiceId { get; init; } = "a0e99841-438c-4a64-b679-ae501e7d6091";
}
