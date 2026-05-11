namespace Clicky.Core.Physics;

/// <summary>
/// Pure-math helpers for the Bezier-arc triangle flight animation.
/// All constants match original Clicky: dist/800 duration clamped to [0.6, 1.4]s,
/// arc height = min(dist·0.2, 80), smoothstep easing, 30%-amplitude scale pulse,
/// and +90° tangent rotation for triangle tip orientation.
/// </summary>
public static class BezierFlight
{
    /// <summary>Minimum flight duration in seconds.</summary>
    public const double MinDurationSeconds = 0.6;

    /// <summary>Maximum flight duration in seconds.</summary>
    public const double MaxDurationSeconds = 1.4;

    /// <summary>Pixels per second used to compute raw flight duration before clamping.</summary>
    public const double SpeedPixelsPerSecond = 800.0;

    /// <summary>Arc height as a fraction of flight distance.</summary>
    public const double ArcHeightFactor = 0.2;

    /// <summary>Maximum arc height in pixels regardless of distance.</summary>
    public const double MaxArcHeightPixels = 80.0;

    /// <summary>Flight duration in seconds for a given Euclidean distance, clamped to [0.6, 1.4].</summary>
    public static double ComputeDuration(double distancePixels)
        => Math.Clamp(distancePixels / SpeedPixelsPerSecond, MinDurationSeconds, MaxDurationSeconds);

    /// <summary>Quadratic Bezier control-point arc height (upward offset from midpoint).</summary>
    public static double ArcHeight(double distancePixels)
        => Math.Min(distancePixels * ArcHeightFactor, MaxArcHeightPixels);

    /// <summary>
    /// Smoothstep easing: t → 3t²−2t³.
    /// Zero first-derivative at t=0 and t=1; symmetric about t=0.5.
    /// </summary>
    public static double Smoothstep(double t)
        => t * t * (3.0 - 2.0 * t);

    /// <summary>
    /// Scale pulse peaking at 1.3× at midpoint and returning to 1.0 at endpoints.
    /// Matches original Clicky: 1 + sin(t·π)·0.3.
    /// </summary>
    public static double ScalePulse(double t)
        => 1.0 + Math.Sin(t * Math.PI) * 0.3;

    /// <summary>Point on a quadratic Bezier curve at parameter t ∈ [0, 1].</summary>
    public static (double x, double y) QuadraticBezier(
        double startX, double startY,
        double controlX, double controlY,
        double endX, double endY,
        double t)
    {
        var s = 1.0 - t;
        return (
            s * s * startX + 2.0 * s * t * controlX + t * t * endX,
            s * s * startY + 2.0 * s * t * controlY + t * t * endY
        );
    }

    /// <summary>Tangent (velocity) direction of the quadratic Bezier at parameter t.</summary>
    public static (double tx, double ty) QuadraticBezierTangent(
        double startX, double startY,
        double controlX, double controlY,
        double endX, double endY,
        double t)
    {
        var s = 1.0 - t;
        return (
            2.0 * s * (controlX - startX) + 2.0 * t * (endX - controlX),
            2.0 * s * (controlY - startY) + 2.0 * t * (endY - controlY)
        );
    }

    /// <summary>
    /// Triangle rotation angle in degrees for a given tangent vector.
    /// Adds 90° so the triangle tip points in the direction of travel.
    /// </summary>
    public static double AngleDeg(double tx, double ty)
        => Math.Atan2(ty, tx) * (180.0 / Math.PI) + 90.0;
}
