using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Arms after a step is delivered. Listens to global mouse presses via IPushToTalkHook.MousePressed.
/// When a left-click (Button1) within <see cref="HitRadiusPx"/> of the target fires, raises ClickConfirmed.
/// Disarms on PTT press, sequence end, or timeout.
/// </summary>
public sealed class StepClickWatcher : IDisposable
{
    public const int HitRadiusPx = 60;
    private const string LeftButtonCode = "Button1";

    private readonly IPushToTalkHook _ptt;
    private volatile bool _armed;
    private int _targetX;
    private int _targetY;

    public event EventHandler? ClickConfirmed;

    public StepClickWatcher(IPushToTalkHook ptt)
    {
        _ptt = ptt;
        _ptt.MousePressed += OnMousePressed;
    }

    /// <summary>Arms the watcher at the given screen coordinates.</summary>
    public void Arm(int targetX, int targetY)
    {
        _targetX = targetX;
        _targetY = targetY;
        _armed = true;
    }

    /// <summary>Disarms without firing ClickConfirmed.</summary>
    public void Disarm() => _armed = false;

    private void OnMousePressed(object? sender, MousePressedEventArgs e)
    {
        if (!_armed) return;
        if (e.ButtonCode != LeftButtonCode) return;

        var dx = e.X - _targetX;
        var dy = e.Y - _targetY;
        if (dx * dx + dy * dy > HitRadiusPx * HitRadiusPx) return;

        _armed = false;
        ClickConfirmed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _armed = false;
        _ptt.MousePressed -= OnMousePressed;
    }
}
