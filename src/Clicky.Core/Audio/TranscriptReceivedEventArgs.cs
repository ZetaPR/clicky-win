namespace Clicky.Core;

/// <summary>Event data for a transcript segment received from the speech-to-text service.</summary>
public sealed class TranscriptReceivedEventArgs : EventArgs
{
    /// <summary>The transcribed text for this segment.</summary>
    public string Text { get; }

    /// <summary>
    /// <c>true</c> when the speaker paused and the segment is finalized;
    /// <c>false</c> for partial (in-progress) results.
    /// </summary>
    public bool IsFinal { get; }

    /// <summary>Initializes a new instance with the given text and finality flag.</summary>
    public TranscriptReceivedEventArgs(string text, bool isFinal)
    {
        Text = text;
        IsFinal = isFinal;
    }
}
