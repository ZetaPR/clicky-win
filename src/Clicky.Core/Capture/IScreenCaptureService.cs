namespace Clicky.Core;

/// <summary>Captures screenshots of the user's displays.</summary>
public interface IScreenCaptureService : IDisposable
{
    /// <summary>
    /// Captures the primary monitor and returns JPEG bytes with dimensions and monitor bounds.
    /// Called on a background thread; implementation must be thread-safe.
    /// </summary>
    Task<ScreenCapture> CaptureAsync(CancellationToken cancellationToken = default);
}
