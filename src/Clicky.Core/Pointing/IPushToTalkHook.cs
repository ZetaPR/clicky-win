namespace Clicky.Core;

/// <summary>Detects the global Ctrl+Win+Space push-to-talk hotkey and raises recording lifecycle events.</summary>
public interface IPushToTalkHook : IDisposable
{
    /// <summary>Fired on a background thread when recording starts.</summary>
    event EventHandler RecordingStarted;

    /// <summary>Fired on a background thread when recording stops.</summary>
    event EventHandler RecordingStopped;

    /// <summary>Fired on a background thread for every global mouse button press.</summary>
    event EventHandler<MousePressedEventArgs>? MousePressed;

    /// <summary>Starts the global keyboard and mouse hook on a background thread.</summary>
    void Start();
}
