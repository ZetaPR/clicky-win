namespace Clicky.Core;

/// <summary>Controls the on-screen cursor overlay.</summary>
public interface IOverlayService
{
    /// <summary>Switch to 5-bar waveform — shown while the mic is recording.</summary>
    void StartListening();

    /// <summary>Switch to spinning arc — shown while waiting for LLM/TTS to complete.</summary>
    void StartProcessing();

    /// <summary>Return to triangle mode, following the mouse cursor.</summary>
    void ReturnToIdle();

    /// <summary>
    /// Animate the cursor to the position indicated by Claude.
    /// <paramref name="claudeX"/>/<paramref name="claudeY"/> are pixel coordinates within the
    /// screenshot that was sent to Claude (dimensions <paramref name="screenshotWidth"/> ×
    /// <paramref name="screenshotHeight"/>).  <paramref name="monitorPhysBounds"/> is the physical
    /// pixel bounds of the monitor the screenshot was taken from, in virtual-screen coordinates.
    /// The implementation converts these to WPF DIPs.
    /// </summary>
    void ShowPointer(
        int claudeX, int claudeY,
        int screenshotWidth, int screenshotHeight,
        MonitorBounds monitorPhysBounds,
        string? label);

    /// <summary>No-op — triangle is always visible; kept for interface compatibility.</summary>
    void HidePointer();

    /// <summary>
    /// Updates overlay element colors immediately.
    /// <paramref name="arrowColor"/> controls the triangle fill,
    /// <paramref name="loaderColor"/> controls the spinner arc gradient end stop,
    /// <paramref name="barsColor"/> controls the waveform bars.
    /// All values are hex color strings (e.g. "#4A9EFF").
    /// </summary>
    void ApplyColors(string arrowColor, string loaderColor, string barsColor);

    /// <summary>
    /// Switches between overlay display modes. "Persistent" keeps the triangle always visible.
    /// "Hidden" hides everything at idle and animates the triangle in on PTT activation.
    /// Takes effect immediately.
    /// </summary>
    void ApplyCursorMode(string mode);

    /// <summary>
    /// Switch from spinner to triangle — called when the AI response is parsed,
    /// before TTS and flight start. Hides the spinner, makes the triangle visible.
    /// </summary>
    void StartResponding();

    /// <summary>
    /// Animate the triangle to the Claude-indicated location, show a speech bubble,
    /// hold for 3 seconds, then fade the bubble. Returns when complete.
    /// Intended to run concurrently with <see cref="ITtsService.SpeakAsync"/>.
    /// <paramref name="monitorPhysBounds"/> is the physical pixel bounds of the monitor the
    /// screenshot was taken from, in virtual-screen coordinates.
    /// </summary>
    Task FlyToAndShowBubbleAsync(
        int claudeX, int claudeY,
        int screenshotWidth, int screenshotHeight,
        MonitorBounds monitorPhysBounds,
        string? label,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pins the triangle at the target coordinate and shows the "Step N of M" badge.
    /// The triangle pulses slowly in this state instead of tracking the mouse cursor.
    /// </summary>
    void StartWaitingForStep(
        int claudeX, int claudeY,
        int screenshotWidth, int screenshotHeight,
        MonitorBounds monitorPhysBounds,
        string? label,
        int stepNumber,
        int totalSteps);

    /// <summary>
    /// Registers the target window HWND for foreground monitoring.
    /// ForegroundLost fires when the user switches away from this window.
    /// Pass nint.Zero to disable monitoring.
    /// </summary>
    void SetTargetHwnd(nint hwnd);

    /// <summary>Fired on the UI thread when the foreground window no longer matches TargetHwnd.</summary>
    event EventHandler? ForegroundLost;
}
