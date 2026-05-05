using Clicky.Core;
using SharpHook;
using SharpHook.Native;

namespace Clicky.Capture.Hotkeys;

/// <summary>
/// Listens for the Ctrl+Win+Space global hotkey via SharpHook and raises
/// <see cref="RecordingStarted"/> / <see cref="RecordingStopped"/> events accordingly.
/// </summary>
public sealed class PushToTalkHook : IPushToTalkHook
{
    private readonly SimpleGlobalHook _hook;
    private readonly HotkeyState _state = new();
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler? RecordingStarted;

    /// <inheritdoc/>
    public event EventHandler? RecordingStopped;

    /// <summary>Initializes the hook with a keyboard-only global listener.</summary>
    public PushToTalkHook()
    {
        // runAsyncOnBackgroundThread: true so RunAsync() spawns its own background thread.
        _hook = new SimpleGlobalHook(runAsyncOnBackgroundThread: true);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <inheritdoc/>
    public void Start() => _hook.RunAsync();

    /// <inheritdoc/>
    public void Stop() => Dispose();

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var (ctrl, win, space) = ClassifyKey(e.Data.KeyCode);
        if (!ctrl && !win && !space) return;

        if (_state.OnKeyDown(ctrl, win, space))
            RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        var (ctrl, win, space) = ClassifyKey(e.Data.KeyCode);
        if (!ctrl && !win && !space) return;

        if (_state.OnKeyUp(ctrl, win, space))
            RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    private static (bool ctrl, bool win, bool space) ClassifyKey(KeyCode key) =>
        key switch
        {
            KeyCode.VcLeftControl or KeyCode.VcRightControl => (true, false, false),
            KeyCode.VcLeftMeta or KeyCode.VcRightMeta => (false, true, false),
            KeyCode.VcSpace => (false, false, true),
            _ => (false, false, false),
        };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;
        _hook.Dispose();
    }
}
