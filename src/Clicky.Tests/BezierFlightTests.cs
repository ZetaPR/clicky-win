using Clicky.Core.Physics;
using Xunit;

namespace Clicky.Tests;

public class BezierFlightTests
{
    // ── ComputeDuration ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeDuration_ZeroDistance_ClampedToMinimum()
    {
        Assert.Equal(BezierFlight.MinDurationSeconds, BezierFlight.ComputeDuration(0));
    }

    [Fact]
    public void ComputeDuration_ShortDistance_ClampedToMinimum()
    {
        // 100px / 800 px/s = 0.125s < 0.6 → clamped to 0.6
        Assert.Equal(0.6, BezierFlight.ComputeDuration(100), precision: 10);
    }

    [Fact]
    public void ComputeDuration_LongDistance_ClampedToMaximum()
    {
        // 2000px / 800 px/s = 2.5s > 1.4 → clamped to 1.4
        Assert.Equal(1.4, BezierFlight.ComputeDuration(2000), precision: 10);
    }

    [Fact]
    public void ComputeDuration_ExactSpeed_NotClamped()
    {
        // 800px / 800 px/s = 1.0s — within [0.6, 1.4]
        Assert.Equal(1.0, BezierFlight.ComputeDuration(800), precision: 10);
    }

    [Fact]
    public void ComputeDuration_AlwaysWithinBounds()
    {
        foreach (var dist in new[] { 0.0, 1.0, 100.0, 480.0, 800.0, 1200.0, 5000.0 })
        {
            var d = BezierFlight.ComputeDuration(dist);
            Assert.InRange(d, BezierFlight.MinDurationSeconds, BezierFlight.MaxDurationSeconds);
        }
    }

    // ── ArcHeight ─────────────────────────────────────────────────────────────

    [Fact]
    public void ArcHeight_SmallDistance_IsProportional()
    {
        // 200px * 0.2 = 40px — below the 80px cap
        Assert.Equal(40.0, BezierFlight.ArcHeight(200), precision: 10);
    }

    [Fact]
    public void ArcHeight_ExactBoundaryDistance_IsMaximum()
    {
        // 400px * 0.2 = 80px — exactly at the cap
        Assert.Equal(BezierFlight.MaxArcHeightPixels, BezierFlight.ArcHeight(400), precision: 10);
    }

    [Fact]
    public void ArcHeight_LargeDistance_ClampedToMaximum()
    {
        // 1000px * 0.2 = 200px > 80px → capped at 80
        Assert.Equal(80.0, BezierFlight.ArcHeight(1000), precision: 10);
    }

    // ── Smoothstep ────────────────────────────────────────────────────────────

    [Fact]
    public void Smoothstep_AtZero_IsZero()
    {
        Assert.Equal(0.0, BezierFlight.Smoothstep(0.0), precision: 10);
    }

    [Fact]
    public void Smoothstep_AtOne_IsOne()
    {
        Assert.Equal(1.0, BezierFlight.Smoothstep(1.0), precision: 10);
    }

    [Fact]
    public void Smoothstep_AtHalf_IsHalf()
    {
        // 0.5² * (3 − 2·0.5) = 0.25 · 2.0 = 0.5
        Assert.Equal(0.5, BezierFlight.Smoothstep(0.5), precision: 10);
    }

    [Fact]
    public void Smoothstep_IsMonotonicallyIncreasing()
    {
        Assert.True(BezierFlight.Smoothstep(0.25) < BezierFlight.Smoothstep(0.5));
        Assert.True(BezierFlight.Smoothstep(0.5) < BezierFlight.Smoothstep(0.75));
    }

    [Fact]
    public void Smoothstep_IsSymmetric_AroundHalf()
    {
        // Smoothstep is symmetric: f(t) + f(1-t) = 1
        for (var t = 0.1; t < 1.0; t += 0.1)
            Assert.Equal(1.0, BezierFlight.Smoothstep(t) + BezierFlight.Smoothstep(1.0 - t), precision: 10);
    }

    // ── ScalePulse ────────────────────────────────────────────────────────────

    [Fact]
    public void ScalePulse_AtZero_IsOne()
    {
        // sin(0) = 0 → 1.0 + 0·0.3 = 1.0
        Assert.Equal(1.0, BezierFlight.ScalePulse(0.0), precision: 10);
    }

    [Fact]
    public void ScalePulse_AtHalf_IsMaximum()
    {
        // sin(π/2) = 1 → 1.0 + 1·0.3 = 1.3
        Assert.Equal(1.3, BezierFlight.ScalePulse(0.5), precision: 10);
    }

    [Fact]
    public void ScalePulse_AtOne_IsOne()
    {
        // sin(π) ≈ 0 → 1.0 + ~0·0.3 ≈ 1.0
        Assert.Equal(1.0, BezierFlight.ScalePulse(1.0), precision: 6);
    }

    [Fact]
    public void ScalePulse_AlwaysGreaterThanOrEqualOne()
    {
        for (var t = 0.0; t <= 1.0; t += 0.05)
            Assert.True(BezierFlight.ScalePulse(t) >= 1.0, $"scale at t={t} should be ≥ 1");
    }

    // ── QuadraticBezier ───────────────────────────────────────────────────────

    [Fact]
    public void QuadraticBezier_AtT0_IsStartPoint()
    {
        var (x, y) = BezierFlight.QuadraticBezier(10, 20, 50, 5, 90, 80, t: 0.0);
        Assert.Equal(10.0, x, precision: 10);
        Assert.Equal(20.0, y, precision: 10);
    }

    [Fact]
    public void QuadraticBezier_AtT1_IsEndPoint()
    {
        var (x, y) = BezierFlight.QuadraticBezier(10, 20, 50, 5, 90, 80, t: 1.0);
        Assert.Equal(90.0, x, precision: 10);
        Assert.Equal(80.0, y, precision: 10);
    }

    [Fact]
    public void QuadraticBezier_AtHalf_CollinearPoints_IsMidpoint()
    {
        // start=(0,0), control=(50,0), end=(100,0): all on X axis
        // at t=0.5: x = 0.25·0 + 2·0.25·50 + 0.25·100 = 0 + 25 + 25 = 50
        var (x, y) = BezierFlight.QuadraticBezier(0, 0, 50, 0, 100, 0, t: 0.5);
        Assert.Equal(50.0, x, precision: 10);
        Assert.Equal(0.0, y, precision: 10);
    }

    [Fact]
    public void QuadraticBezier_ControlAboveMidpoint_PeaksAboveBaseline()
    {
        // start=(0,100), end=(100,100), control=(50,0) — control is above (lower Y = higher on screen)
        // at t=0.5: y = 0.25·100 + 0.5·0 + 0.25·100 = 25 + 0 + 25 = 50 < 100
        var (_, y) = BezierFlight.QuadraticBezier(0, 100, 50, 0, 100, 100, t: 0.5);
        Assert.True(y < 100, "curve should peak above (lower Y than) the baseline");
    }

    // ── QuadraticBezierTangent ────────────────────────────────────────────────

    [Fact]
    public void QuadraticBezierTangent_AtT0_PointsTowardControl()
    {
        // start=(0,0), control=(1,0), end=(2,0)
        // tangent at t=0: 2*(1-0)*(control - start) = 2*(1,0) → direction=(1,0)
        var (tx, ty) = BezierFlight.QuadraticBezierTangent(0, 0, 1, 0, 2, 0, t: 0.0);
        Assert.True(tx > 0, "tangent x should point toward control");
        Assert.Equal(0.0, ty, precision: 10);
    }

    [Fact]
    public void QuadraticBezierTangent_AtT1_PointsFromControlToEnd()
    {
        // start=(0,0), control=(1,0), end=(2,0)
        // tangent at t=1: 2*(2-1)=(2,0) → direction (positive x)
        var (tx, _) = BezierFlight.QuadraticBezierTangent(0, 0, 1, 0, 2, 0, t: 1.0);
        Assert.True(tx > 0);
    }

    // ── AngleDeg ─────────────────────────────────────────────────────────────

    [Fact]
    public void AngleDeg_HorizontalRightTangent_Is90Degrees()
    {
        // atan2(0, 1) = 0° + 90° offset = 90°
        Assert.Equal(90.0, BezierFlight.AngleDeg(tx: 1, ty: 0), precision: 10);
    }

    [Fact]
    public void AngleDeg_DownwardTangent_Is180Degrees()
    {
        // atan2(1, 0) = 90° + 90° offset = 180°
        Assert.Equal(180.0, BezierFlight.AngleDeg(tx: 0, ty: 1), precision: 10);
    }

    [Fact]
    public void AngleDeg_DiagonalSouthEastTangent_Is135Degrees()
    {
        // atan2(1, 1) = 45° + 90° offset = 135°
        Assert.Equal(135.0, BezierFlight.AngleDeg(tx: 1, ty: 1), precision: 6);
    }

    [Fact]
    public void AngleDeg_UpwardTangent_Is0Degrees()
    {
        // atan2(-1, 0) = -90° + 90° offset = 0°
        Assert.Equal(0.0, BezierFlight.AngleDeg(tx: 0, ty: -1), precision: 10);
    }
}
