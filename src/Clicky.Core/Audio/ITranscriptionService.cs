namespace Clicky.Core;

/// <summary>Streams audio chunks to a speech-to-text service and raises transcript events.</summary>
public interface ITranscriptionService : IDisposable
{
    /// <summary>
    /// Fired on a background thread when a transcript segment arrives.
    /// <c>IsFinal=true</c> means the speaker paused and the segment is complete.
    /// </summary>
    event EventHandler<TranscriptReceivedEventArgs> TranscriptReceived;

    /// <summary>Opens the WebSocket connection and starts the background receive loop.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends a PCM audio chunk to the service.</summary>
    Task SendAudioAsync(byte[] pcmData, CancellationToken cancellationToken = default);

    /// <summary>Sends the terminate_session signal, waits for the final transcript, then closes the connection.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
