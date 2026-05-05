namespace Clicky.Core;

/// <summary>Detects the global Ctrl+Win+Space push-to-talk hotkey and raises recording lifecycle events.</summary>
public interface IPushToTalkHook : IDisposable
{
    /// <summary>Fired on a background thread when recording starts. Callers must marshal to the UI thread if needed.</summary>
    event EventHandler RecordingStarted;

    /// <summary>Fired on a background thread when recording stops. Callers must marshal to the UI thread if needed.</summary>
    event EventHandler RecordingStopped;

    /// <summary>Starts the global keyboard hook on a background thread.</summary>
    void Start();
}
