namespace Clicky.Core;

/// <summary>Runtime configuration for the companion.</summary>
public sealed class CompanionSettings
{
    public string WorkerUrl { get; init; } = "https://httpbin.org/post";
}
