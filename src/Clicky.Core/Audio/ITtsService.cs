namespace Clicky.Core;

/// <summary>Synthesizes text to speech and plays it through the default audio output device.</summary>
public interface ITtsService : IDisposable
{
    /// <summary>
    /// Synthesizes <paramref name="text"/> and plays it through the default audio output device.
    /// Awaiting completes when playback is done. Cancellation stops playback immediately.
    /// </summary>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
