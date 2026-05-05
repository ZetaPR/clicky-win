namespace Clicky.Core;

/// <summary>Detects the global Ctrl+Win+Space push-to-talk hotkey and raises recording lifecycle events.</summary>
public interface IPushToTalkHook : IDisposable
{
    /// <summary>Raised when all three hotkey keys are simultaneously held down.</summary>
    event EventHandler RecordingStarted;

    /// <summary>Raised when any of the three hotkey keys is released while recording.</summary>
    event EventHandler RecordingStopped;

    /// <summary>Starts the global keyboard hook on a background thread.</summary>
    void Start();

    /// <summary>Stops the global keyboard hook and releases resources.</summary>
    void Stop();
}
