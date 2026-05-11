using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Clicky.Core;
using Clicky.Core.Physics;
using Serilog;

namespace Clicky.Overlay;

/// <summary>
/// Full-virtual-screen transparent click-through overlay hosting the animated blue triangle cursor.
/// Three states:
///   Idle      — blue triangle tracks the real mouse cursor (offset to the side)
///   Listening — 5-bar waveform animation while mic is recording
///   Processing — spinning arc while waiting for LLM/TTS
/// On ShowPointer the triangle flies via Bezier arc to the target, then resumes tracking.
/// The window spans the entire virtual screen so it works on any monitor in a multi-monitor setup.
/// </summary>
public sealed partial class CursorOverlayWindow : Window, IOverlayService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    // Offset from real cursor tip: buddy sits 35px right and 25px above the cursor (matches original Clicky).
    private const double CursorOffsetX = 35.0;
    private const double CursorOffsetY = 25.0;

    // Extra window space beyond the virtual screen edges — safety net for spring overshoot.
    private const double RightExtra  = 100.0;
    private const double BottomExtra = 100.0;
    private const double TopExtra    = 75.0;

    // Rotated-triangle body extents from anchor (DIPs), derived from triangle points (0,0)/(-9,21)/(9,21)
    // rotated by IdleAngleDeg ≈ -125.5°. Right extent ≈ 22.3, top extent ≈ 19.6 — rounded up with buffer.
    private const double TriangleRightExtent = 35.0; // keeps triangle body clear of right edge + spring buffer
    private const double TriangleTopExtent   = 30.0; // keeps triangle body clear of top  edge + spring buffer

    // Idle rotation: atan2(-CursorOffsetX, -CursorOffsetY) * 180/π ≈ -125.5°.
    // This makes the triangle TIP point toward the cursor — the body extends upper-right,
    // tip aims lower-left at the real cursor position.
    private static readonly double IdleAngleDeg =
        Math.Atan2(-CursorOffsetX, -CursorOffsetY) * (180.0 / Math.PI);

    // GetSystemMetrics indices for the physical virtual-screen rectangle.
    // These always return physical pixels regardless of process DPI awareness.
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const uint SWP_NOZORDER   = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private IntPtr _hwnd;
    private bool _hiddenMode;       // true = Mode 2: hidden at idle, animates in on PTT
    private bool _flyoutActive;     // Mode 2 only: triangle is springing from cursor → buddy offset
    private int _flyoutFrames;
    // Triangle spring-out takes ~300ms; bars replace it once it has settled at the buddy position.
    private const int FlyoutDurationFrames = 18;

    private enum OverlayState { Idle, Listening, Processing }

    private OverlayState _state = OverlayState.Idle;
    private Point _currentPos;
    private bool _isFlying;

    // Waveform animation
    private double _wavePhase;
    private static readonly double[] WaveBarProfiles = [0.4, 0.7, 1.0, 0.7, 0.4];
    private const double WaveBarWidth = 3.0;
    private const double WaveBarGap = 3.0;
    private const double WaveMaxHeight = 24.0;
    private const double WaveMinHeight = 4.0;

    // Bezier flight
    private int _animFrame;
    private int _animTotalFrames;
    private Point _animStart;
    private Point _animEnd;
    private Point _animControl;

    private readonly DispatcherTimer _mainTimer;  // 60fps — mouse tracking + all animations
    private readonly DispatcherTimer _animTimer;  // 60fps — Bezier flight
    private readonly DispatcherTimer _dwellTimer; // one-shot — ends the dwell after pointing

    private TaskCompletionSource? _flightTcs;
    private static readonly string[] PointerPhrases =
        ["right here!", "this one!", "over here!", "click this!", "here it is!", "found it!"];
    private static readonly Random _rng = new();

    // Spring physics — delegates to SpringSimulator (response=0.2, dampingFraction=0.6)
    private SpringSimulator _spring;

    public CursorOverlayWindow()
    {
        InitializeComponent();

        // Set an approximate initial position — RepositionToVirtualScreen (called from Loaded)
        // will correct it to exact physical pixels once we have the HWND and DPI scale.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop - TopExtra;
        Width  = SystemParameters.VirtualScreenWidth + RightExtra;
        Height = SystemParameters.VirtualScreenHeight + TopExtra;

        _currentPos = new Point(Width / 2, Height / 2);
        ApplyTriangleTransform(_currentPos, 0, 1.0);

        // Defer reposition until after WPF's own Loaded layout pass completes; otherwise
        // WPF's layout engine fires SetWindowPos again after ours and reverts the size.
        Loaded     += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, RepositionToVirtualScreen);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, RepositionToVirtualScreen);

        _mainTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 60.0)
        };
        _mainTimer.Tick += OnMainTick;
        _mainTimer.Start();

        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 60.0)
        };
        _animTimer.Tick += OnAnimTick;

        _dwellTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dwellTimer.Tick += OnDwellTick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    // 60fps tick — mouse tracking + waveform + spinner updates
    private void OnMainTick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var pt)) return;
        if (!GetWindowRect(_hwnd, out var winRect)) return;

        var source = PresentationSource.FromVisual(this);
        var m11 = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var m22 = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        var raw = new Point((pt.X - winRect.Left) * m11, (pt.Y - winRect.Top) * m22);

        // Track glow at cursor in hidden mode whenever it is visible
        if (_hiddenMode && CursorGlow.Opacity > 0)
        {
            Canvas.SetLeft(CursorGlow, raw.X - CursorGlow.Width  / 2);
            Canvas.SetTop (CursorGlow, raw.Y - CursorGlow.Height / 2);
        }

        switch (_state)
        {
            case OverlayState.Idle when !_isFlying:
            {
                if (_hiddenMode) break; // nothing to update when hidden at idle
                var mon  = GetMonitorRect(pt.X, pt.Y);
                var maxX = (mon.Right  - winRect.Left) * m11 - TriangleRightExtent;
                var minY = (mon.Top    - winRect.Top)  * m22 + TriangleTopExtent;
                var maxY = (mon.Bottom - winRect.Top)  * m22 - 5.0;
                var tgtX = Math.Min(raw.X + CursorOffsetX, maxX);
                var tgtY = Math.Clamp(raw.Y - CursorOffsetY, minY, maxY);
                var (nx, ny) = _spring.Step(_currentPos.X, _currentPos.Y, tgtX, tgtY);
                // Hard-wall: prevent spring overshoot from pushing the triangle past any edge
                if (nx > maxX) { nx = maxX; _spring.VelocityX = 0; }
                if (ny < minY) { ny = minY; _spring.VelocityY = 0; }
                if (ny > maxY) { ny = maxY; _spring.VelocityY = 0; }
                _currentPos = new Point(nx, ny);
                ApplyTriangleTransform(_currentPos, IdleAngleDeg, 1.0);
                break;
            }

            case OverlayState.Listening:
            {
                var mon  = GetMonitorRect(pt.X, pt.Y);
                var maxX = (mon.Right  - winRect.Left) * m11 - TriangleRightExtent;
                var minY = (mon.Top    - winRect.Top)  * m22 + TriangleTopExtent;
                var maxY = (mon.Bottom - winRect.Top)  * m22 - 5.0;
                var tgtX = Math.Min(raw.X + CursorOffsetX, maxX);
                var tgtY = Math.Clamp(raw.Y - CursorOffsetY, minY, maxY);
                var (nx, ny) = _spring.Step(_currentPos.X, _currentPos.Y, tgtX, tgtY);
                if (nx > maxX) { nx = maxX; _spring.VelocityX = 0; }
                if (ny < minY) { ny = minY; _spring.VelocityY = 0; }
                if (ny > maxY) { ny = maxY; _spring.VelocityY = 0; }
                _currentPos = new Point(nx, ny);

                if (_flyoutActive)
                {
                    // Mode 2 fly-out: triangle springs from cursor to buddy offset.
                    // After FlyoutDurationFrames the spring has mostly settled — switch to bars.
                    ApplyTriangleTransform(_currentPos, IdleAngleDeg, 1.0);
                    if (++_flyoutFrames >= FlyoutDurationFrames)
                    {
                        _flyoutActive = false;
                        Triangle.Opacity = 0;
                        SetBarsOpacity(1);
                    }
                }
                else
                {
                    UpdateWaveform(_currentPos);
                }
                break;
            }

            case OverlayState.Processing:
                _currentPos = new Point(raw.X + CursorOffsetX, raw.Y - CursorOffsetY);
                SpinnerRotate.Angle = (SpinnerRotate.Angle + 7.5) % 360; // 450°/s
                Canvas.SetLeft(Spinner, _currentPos.X - 7);
                Canvas.SetTop(Spinner, _currentPos.Y - 7);
                break;
        }
    }

    // Positions the HWND so it covers the exact physical virtual screen rectangle plus buffers.
    // GetSystemMetrics(SM_xVIRTUALSCREEN) always returns physical pixels, bypassing WPF's
    // DPI-scaled Left/Width properties which produce the wrong physical position on multi-DPI setups.
    private void RepositionToVirtualScreen()
    {
        if (_hwnd == IntPtr.Zero) return;
        var source = PresentationSource.FromVisual(this);
        var m11 = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var m22 = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        var physLeft   = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var physTop    = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var physWidth  = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var physHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        var physTopExtra    = (int)Math.Ceiling(TopExtra    / m22);
        var physBottomExtra = (int)Math.Ceiling(BottomExtra / m22);
        var physRightExtra  = (int)Math.Ceiling(RightExtra  / m11);

        SetWindowPos(
            _hwnd, IntPtr.Zero,
            physLeft, physTop - physTopExtra,
            physWidth + physRightExtra, physHeight + physTopExtra + physBottomExtra,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private RECT GetMonitorRect(int physX, int physY)
    {
        const uint MONITOR_DEFAULTTONEAREST = 2;
        var hMon = MonitorFromPoint(new POINT { X = physX, Y = physY }, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        return GetMonitorInfo(hMon, ref mi)
            ? mi.rcMonitor
            : new RECT { Left = -8192, Top = -8192, Right = 8192, Bottom = 8192 };
    }

    private void UpdateWaveform(Point pos)
    {
        _wavePhase += 1.0 / 60.0;
        const double totalWidth = 5 * WaveBarWidth + 4 * WaveBarGap; // 27px
        double startX = pos.X - totalWidth / 2.0;

        var bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };
        for (int i = 0; i < 5; i++)
        {
            var height = WaveMinHeight + (WaveMaxHeight - WaveMinHeight) * WaveBarProfiles[i]
                         * (0.5 + 0.5 * Math.Sin(_wavePhase * 8.0 + i * 0.7));
            bars[i].Height = height;
            Canvas.SetLeft(bars[i], startX + i * (WaveBarWidth + WaveBarGap));
            Canvas.SetTop(bars[i], pos.Y - height / 2.0);
        }
    }

    // ── Public state transitions ────────────────────────────────────────────

    /// <inheritdoc/>
    public void StartListening()
    {
        Dispatcher.Invoke(() =>
        {
            _state = OverlayState.Listening;
            _isFlying = false;
            _animTimer.Stop();
            _dwellTimer.Stop();
            _flightTcs?.TrySetCanceled();
            _flightTcs = null;
            _wavePhase = 0;
            _spring.Reset();
            Spinner.Opacity = 0;
            SpeechBubble.Opacity = 0;

            if (_hiddenMode)
            {
                // Place triangle at the cursor tip and let the spring carry it to buddy offset.
                // Bars are hidden during the fly-out; OnMainTick switches them on after settling.
                if (GetCursorPos(out var pt) && GetWindowRect(_hwnd, out var wr))
                {
                    var src = PresentationSource.FromVisual(this);
                    var m11 = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
                    var m22 = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
                    _currentPos = new Point((pt.X - wr.Left) * m11, (pt.Y - wr.Top) * m22);
                }
                _flyoutActive  = true;
                _flyoutFrames  = 0;
                CursorGlow.Opacity = 1;
                Triangle.Opacity   = 1;
                SetBarsOpacity(0);
            }
            else
            {
                Triangle.Opacity = 0;
                SetBarsOpacity(1);
            }
        });
    }

    /// <inheritdoc/>
    public void StartProcessing()
    {
        Dispatcher.Invoke(() =>
        {
            _state = OverlayState.Processing;
            _flyoutActive = false;
            _animTimer.Stop();
            _dwellTimer.Stop();
            Triangle.Opacity = 0;
            SetBarsOpacity(0);
            Spinner.Opacity = 1;
            // Glow stays visible in hidden mode — cursor lit while AI processes
        });
    }

    /// <inheritdoc/>
    public void ReturnToIdle()
    {
        Dispatcher.Invoke(() =>
        {
            _state = OverlayState.Idle;
            _isFlying = false;
            _flyoutActive = false;
            _spring.Reset();
            SetBarsOpacity(0);
            Spinner.Opacity = 0;
            SpeechBubble.Opacity = 0;
            CursorGlow.Opacity = 0;
            Triangle.Opacity = _hiddenMode ? 0 : 1;
        });
    }

    /// <inheritdoc/>
    public void StartResponding()
    {
        Dispatcher.Invoke(() =>
        {
            _state = OverlayState.Idle;
            _isFlying = false;
            _flyoutActive = false;
            _spring.Reset();
            _animTimer.Stop();
            _dwellTimer.Stop();
            // Triangle flies to target — hide glow so triangle is the visual focus
            CursorGlow.Opacity = 0;
            Triangle.Opacity = 1;
            SetBarsOpacity(0);
            Spinner.Opacity = 0;
            SpeechBubble.Opacity = 0;
        });
    }

    /// <inheritdoc/>
    public void ShowPointer(
        int claudeX, int claudeY,
        int screenshotWidth, int screenshotHeight,
        MonitorBounds monitorPhysBounds,
        string? label)
    {
        Dispatcher.Invoke(() =>
        {
            _state = OverlayState.Idle;
            _isFlying = true;
            _animTimer.Stop();
            _dwellTimer.Stop();
            Triangle.Opacity = 1;
            SetBarsOpacity(0);
            Spinner.Opacity = 0;

            var destination = ClaudeCoordToWpfDip(claudeX, claudeY, screenshotWidth, screenshotHeight, monitorPhysBounds);
            StartFlight(_currentPos, destination);
            _dwellTimer.Start();
        });
    }

    /// <inheritdoc/>
    public async Task FlyToAndShowBubbleAsync(
        int claudeX, int claudeY,
        int screenshotWidth, int screenshotHeight,
        MonitorBounds monitorPhysBounds,
        string? label,
        CancellationToken cancellationToken)
    {
        var landedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Point destination = default;
        Dispatcher.Invoke(() =>
        {
            destination = ClaudeCoordToWpfDip(claudeX, claudeY, screenshotWidth, screenshotHeight, monitorPhysBounds);
            _state = OverlayState.Idle;
            _isFlying = true;
            _animTimer.Stop();
            _dwellTimer.Stop();
            Triangle.Opacity = 1;
            SetBarsOpacity(0);
            Spinner.Opacity = 0;
            SpeechBubble.Opacity = 0;
            _flightTcs = landedTcs;
            StartFlight(_currentPos, destination);
        });

        try
        {
            await landedTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => { _flightTcs = null; SpeechBubble.Opacity = 0; });
            throw;
        }

        var text = !string.IsNullOrWhiteSpace(label)
            ? label
            : PointerPhrases[_rng.Next(PointerPhrases.Length)];

        await ShowSpeechBubbleAsync(destination, text, cancellationToken).ConfigureAwait(false);
        await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
        await HideSpeechBubbleAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ShowSpeechBubbleAsync(Point pos, string text, CancellationToken ct)
    {
        Dispatcher.Invoke(() =>
        {
            BubbleText.Text = "";
            Canvas.SetLeft(SpeechBubble, pos.X + 15);
            Canvas.SetTop(SpeechBubble, pos.Y - 20);
            BubbleScale.ScaleX = 0.5;
            BubbleScale.ScaleY = 0.5;
            SpeechBubble.Opacity = 1;

            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                0.5, 1.0, new Duration(TimeSpan.FromMilliseconds(300)))
            {
                EasingFunction = new System.Windows.Media.Animation.BackEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                    Amplitude = 0.3
                }
            };
            BubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            BubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        });

        foreach (var ch in text)
        {
            ct.ThrowIfCancellationRequested();
            Dispatcher.Invoke(() => BubbleText.Text += ch);
            await Task.Delay(_rng.Next(30, 61), ct).ConfigureAwait(false);
        }
    }

    private async Task HideSpeechBubbleAsync(CancellationToken ct)
    {
        var fadeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Invoke(() =>
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(500)));
            anim.Completed += (_, _) => fadeTcs.TrySetResult();
            SpeechBubble.BeginAnimation(UIElement.OpacityProperty, anim);
        });
        await fadeTcs.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void HidePointer() { /* triangle is always visible */ }

    /// <inheritdoc/>
    public void ApplyColors(string arrowColor, string loaderColor, string barsColor)
    {
        Dispatcher.Invoke(() =>
        {
            Triangle.Fill = new SolidColorBrush(ParseColor(arrowColor));

            if (Spinner.Stroke is LinearGradientBrush lgb)
            {
                if (lgb.IsFrozen) { lgb = lgb.Clone(); Spinner.Stroke = lgb; }
                if (lgb.GradientStops.Count > 1)
                    lgb.GradientStops[1].Color = ParseColor(loaderColor);
            }

            var barsBrush = new SolidColorBrush(ParseColor(barsColor));
            Bar1.Fill = Bar2.Fill = Bar3.Fill = Bar4.Fill = Bar5.Fill = barsBrush;

            if (CursorGlow.Fill is RadialGradientBrush glow)
            {
                if (glow.IsFrozen) { glow = glow.Clone(); CursorGlow.Fill = glow; }
                var c = ParseColor(arrowColor);
                glow.GradientStops[0].Color = Color.FromArgb(0x99, c.R, c.G, c.B);
                glow.GradientStops[1].Color = Color.FromArgb(0x33, c.R, c.G, c.B);
            }
        });
    }

    /// <inheritdoc/>
    public void ApplyCursorMode(string mode)
    {
        Dispatcher.Invoke(() =>
        {
            _hiddenMode = mode == "Hidden";
            _flyoutActive = false;
            CursorGlow.Opacity = 0;
            // When switching modes, sync triangle visibility to current idle state
            if (_state == OverlayState.Idle && !_isFlying)
                Triangle.Opacity = _hiddenMode ? 0 : 1;
        });
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x4A, 0x9E, 0xFF); }
    }

    private void OnDwellTick(object? sender, EventArgs e)
    {
        _dwellTimer.Stop();
        _isFlying = false;
        SpeechBubble.Opacity = 0;
    }

    // ── Bezier flight ───────────────────────────────────────────────────────

    private void StartFlight(Point from, Point to)
    {
        _animStart = from;
        _animEnd = to;

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < 1.0)
        {
            _currentPos = to;
            ApplyTriangleTransform(to, IdleAngleDeg, 1.0);
            _flightTcs?.TrySetResult();
            _flightTcs = null;
            return;
        }

        var durationSeconds = BezierFlight.ComputeDuration(distance);
        _animTotalFrames = (int)(durationSeconds * 60.0);
        _animFrame = 0;

        var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        _animControl = new Point(mid.X, mid.Y - BezierFlight.ArcHeight(distance));

        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        _animFrame++;

        if (_animFrame > _animTotalFrames)
        {
            _animTimer.Stop();
            _currentPos = _animEnd;
            ApplyTriangleTransform(_animEnd, IdleAngleDeg, 1.0);
            _flightTcs?.TrySetResult();
            _flightTcs = null;
            return;
        }

        var linear = (double)_animFrame / _animTotalFrames;
        var t = BezierFlight.Smoothstep(linear);

        var (x, y) = BezierFlight.QuadraticBezier(
            _animStart.X, _animStart.Y,
            _animControl.X, _animControl.Y,
            _animEnd.X, _animEnd.Y, t);
        _currentPos = new Point(x, y);

        var (btx, bty) = BezierFlight.QuadraticBezierTangent(
            _animStart.X, _animStart.Y,
            _animControl.X, _animControl.Y,
            _animEnd.X, _animEnd.Y, t);
        var angle = BezierFlight.AngleDeg(btx, bty);
        var scale = BezierFlight.ScalePulse(linear);

        ApplyTriangleTransform(_currentPos, angle, scale);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts Claude's coordinate (in the screenshot's pixel space) to WPF DIP canvas space.
    /// The screenshot was captured from <paramref name="mon"/> (physical pixel bounds in virtual-screen
    /// coordinates). We map the Claude coordinate into that monitor's physical pixel space, then
    /// convert to DIPs relative to the overlay window's top-left corner.
    /// </summary>
    private Point ClaudeCoordToWpfDip(int claudeX, int claudeY, int shotW, int shotH, MonitorBounds mon)
    {
        if (shotW <= 0 || shotH <= 0) return new Point(0, 0);
        var physX = mon.X + (int)(claudeX * (double)mon.Width  / shotW);
        var physY = mon.Y + (int)(claudeY * (double)mon.Height / shotH);
        var dip = CursorPhysToWpfDip(physX, physY);
        Log.Debug(
            "[Overlay] ShowPointer claude=({CX},{CY}) shot={SW}x{SH} mon=({MX},{MY},{MW},{MH}) -> dip=({DX},{DY})",
            claudeX, claudeY, shotW, shotH, mon.X, mon.Y, mon.Width, mon.Height, (int)dip.X, (int)dip.Y);
        return dip;
    }

    /// <summary>
    /// Converts physical coordinates to WPF DIPs relative to the overlay canvas origin.
    /// Uses GetWindowRect for the physical window origin so the result is correct on any DPI scale.
    /// </summary>
    private Point CursorPhysToWpfDip(int physX, int physY)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null || !GetWindowRect(_hwnd, out var winRect))
            return new Point(physX - Left, physY - Top);
        var m11 = source.CompositionTarget.TransformFromDevice.M11;
        var m22 = source.CompositionTarget.TransformFromDevice.M22;
        return new Point((physX - winRect.Left) * m11, (physY - winRect.Top) * m22);
    }

    private void ApplyTriangleTransform(Point pos, double angleDeg, double scale)
    {
        TrianglePos.X = pos.X;
        TrianglePos.Y = pos.Y;
        TriangleRotate.Angle = angleDeg;
        TriangleScale.ScaleX = scale;
        TriangleScale.ScaleY = scale;
    }

    private void SetBarsOpacity(double opacity)
    {
        Bar1.Opacity = Bar2.Opacity = Bar3.Opacity = Bar4.Opacity = Bar5.Opacity = opacity;
    }

    protected override void OnClosed(EventArgs e)
    {
        _mainTimer.Stop();
        _mainTimer.Tick -= OnMainTick;
        _animTimer.Stop();
        _animTimer.Tick -= OnAnimTick;
        _dwellTimer.Stop();
        _dwellTimer.Tick -= OnDwellTick;
        _flightTcs?.TrySetCanceled();
        base.OnClosed(e);
    }
}
