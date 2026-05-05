namespace Clicky.Core;

/// <summary>
/// Captures audio from the system microphone as PCM data.
/// Fired events carry a copy of the audio buffer — safe to use after the event returns.
/// </summary>
public interface IMicrophoneRecorder : IDisposable
{
    /// <summary>Fired on a background thread when audio PCM data is available. Callers must marshal to the UI thread if needed.</summary>
    event EventHandler<byte[]> AudioDataAvailable;

    /// <summary>Starts capturing audio from the microphone.</summary>
    void Start();

    /// <summary>Stops capturing audio from the microphone.</summary>
    void Stop();
}
