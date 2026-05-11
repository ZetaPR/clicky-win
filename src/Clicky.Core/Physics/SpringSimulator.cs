namespace Clicky.Core.Physics;

/// <summary>
/// Critically-damped spring simulator matching original Clicky spring animation:
/// response=0.2s, dampingFraction=0.6.
/// ω = 2π/0.2 ≈ 31.4 rad/s → k = ω² ≈ 987, b = 2ωζ ≈ 37.7.
/// Runs at 60fps (Dt = 1/60). Caller owns one instance per tracked point.
/// </summary>
public struct SpringSimulator
{
    /// <summary>Spring stiffness. k = ω² where ω = 2π / response.</summary>
    public const double K = 987.0;

    /// <summary>Damping coefficient. b = 2·ω·dampingFraction.</summary>
    public const double B = 37.7;

    /// <summary>Integration time step for 60fps.</summary>
    public const double Dt = 1.0 / 60.0;

    /// <summary>Current X-axis velocity in units/second.</summary>
    public double VelocityX;

    /// <summary>Current Y-axis velocity in units/second.</summary>
    public double VelocityY;

    /// <summary>
    /// Advances the spring one time step (1/60 s) using Euler integration.
    /// Updates <see cref="VelocityX"/>/<see cref="VelocityY"/> in-place and
    /// returns the new position.
    /// </summary>
    public (double x, double y) Step(double currentX, double currentY, double targetX, double targetY)
    {
        var ax = (targetX - currentX) * K - VelocityX * B;
        var ay = (targetY - currentY) * K - VelocityY * B;
        VelocityX += ax * Dt;
        VelocityY += ay * Dt;
        return (currentX + VelocityX * Dt, currentY + VelocityY * Dt);
    }

    /// <summary>Zeroes velocity — call on state transitions to prevent residual drift.</summary>
    public void Reset()
    {
        VelocityX = 0;
        VelocityY = 0;
    }
}
