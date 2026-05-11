namespace Clicky.Core;

/// <summary>Sends a screen capture and spoken transcript to an LLM and streams back text deltas.</summary>
public interface ILlmService
{
    /// <summary>
    /// Streams text deltas from the LLM. Each yielded string is a small piece of the response.
    /// The stream ends when the LLM finishes or the cancellation token fires.
    /// <paramref name="screenshotWidth"/> and <paramref name="screenshotHeight"/> are the pixel
    /// dimensions of the JPEG — used by the worker to resolve coordinate percentages accurately.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(
        byte[] screenshot,
        string transcript,
        int screenshotWidth,
        int screenshotHeight,
        CancellationToken cancellationToken = default);
}
