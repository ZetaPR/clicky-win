using Clicky.Core;
using Serilog;
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

    /// <inheritdoc/>
    // CS0067 suppressed: MousePressed is wired to SharpHook in Task 8.
#pragma warning disable CS0067
    public event EventHandler<MousePressedEventArgs>? MousePressed;
#pragma warning restore CS0067

    /// <summary>Initializes the hook with a keyboard-only global listener.</summary>
    public PushToTalkHook()
    {
        // GlobalHookType.Keyboard limits the hook to keyboard events only, avoiding unnecessary
        // mouse hook overhead. Provider is null to use the default libuiohook provider.
        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard, globalHookProvider: null, runAsyncOnBackgroundThread: true);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <inheritdoc/>
    public void Start()
    {
        _ = _hook.RunAsync().ContinueWith(
            t => Log.Error(t.Exception, "Push-to-talk hook failed to run"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

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
