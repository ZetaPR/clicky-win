using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Holds the active multi-step plan in memory. Tracks step index, original screenshot,
/// conversation history, and target HWND. Clears itself on timeout (default 30s) or when
/// Clear() is called.
/// </summary>
public sealed class StepPlanStore : IDisposable
{
    private readonly double _timeoutSeconds;
    private readonly List<Step> _steps = [];
    private readonly List<LlmMessage> _history = [];
    private int _currentIndex;
    private ScreenCapture? _originalCapture;
    private nint _targetHwnd;
    private Timer? _timeoutTimer;
    private volatile bool _isActive;

    public event EventHandler? TimedOut;

    public StepPlanStore(double timeoutSeconds = 30.0)
        => _timeoutSeconds = timeoutSeconds;

    public bool IsActive => _isActive;
    public ScreenCapture? OriginalCapture => _originalCapture;
    public nint TargetHwnd => _targetHwnd;
    public IReadOnlyList<LlmMessage> History => _history;
    public IReadOnlyList<Step> AllSteps => _steps;

    public Step? CurrentStep
    {
        get
        {
            if (!_isActive || _currentIndex >= _steps.Count) return null;
            return _steps[_currentIndex];
        }
    }

    public int TotalSteps => _steps.Count;

    public void Load(ScreenCapture capture, nint targetHwnd)
    {
        Clear();
        _originalCapture = capture;
        _targetHwnd = targetHwnd;
        _isActive = true;
        ResetTimeout();
    }

    public void AddStep(Step step)
    {
        _steps.Add(step);
    }

    public void AppendHistory(LlmMessage message)
    {
        _history.Add(message);
    }

    /// <summary>
    /// Advances to step at <paramref name="newIndex"/>, updating its coords if provided
    /// (lazy coordinate resolution from the verify response).
    /// </summary>
    public void AdvanceTo(int newIndex, int? nextX, int? nextY, string? nextLabel)
    {
        if (newIndex >= _steps.Count) return;

        if (nextX.HasValue || nextY.HasValue || nextLabel is not null)
        {
            var s = _steps[newIndex];
            _steps[newIndex] = new Step
            {
                Number = s.Number,
                Text = s.Text,
                X = nextX ?? s.X,
                Y = nextY ?? s.Y,
                Label = nextLabel ?? s.Label,
            };
        }

        _currentIndex = newIndex;
        ResetTimeout();
    }

    public void Clear()
    {
        _isActive = false;
        _steps.Clear();
        _history.Clear();
        _currentIndex = 0;
        _originalCapture = null;
        _targetHwnd = nint.Zero;
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }

    private void ResetTimeout()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = new Timer(_ =>
        {
            Clear();
            TimedOut?.Invoke(this, EventArgs.Empty);
        }, null, TimeSpan.FromSeconds(_timeoutSeconds), Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }
}
