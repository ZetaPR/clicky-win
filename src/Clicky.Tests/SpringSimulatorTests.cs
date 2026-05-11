using Clicky.Core.Physics;
using Xunit;

namespace Clicky.Tests;

public class SpringSimulatorTests
{
    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void K_ApproximatesOmegaSquared_ForResponse0Point2()
    {
        // Original Clicky: response=0.2 → ω = 2π/0.2 = 10π ≈ 31.416, k = ω² ≈ 986.96
        var omega = 2.0 * Math.PI / 0.2;
        var omegaSquared = omega * omega; // ≈ 986.96

        // K should round to the same integer as ω²
        Assert.Equal(SpringSimulator.K, omegaSquared, precision: 0);
        Assert.Equal(987.0, SpringSimulator.K);
    }

    [Fact]
    public void B_ApproximatesTwiceOmegaDampingFraction_ForDamping0Point6()
    {
        // Original Clicky: dampingFraction=0.6 → b = 2·ω·0.6 = 12π ≈ 37.699
        var omega = 2.0 * Math.PI / 0.2;
        var theoretical = 2.0 * omega * 0.6; // ≈ 37.699

        Assert.Equal(SpringSimulator.B, theoretical, precision: 1);
        Assert.Equal(37.7, SpringSimulator.B);
    }

    [Fact]
    public void Dt_IsOneOver60()
    {
        Assert.Equal(1.0 / 60.0, SpringSimulator.Dt, precision: 10);
    }

    // ── Step: motion toward target ────────────────────────────────────────────

    [Fact]
    public void Step_FromRestAtOrigin_MovesTowardPositiveTarget()
    {
        var spring = new SpringSimulator();

        var (newX, newY) = spring.Step(currentX: 0, currentY: 0, targetX: 100, targetY: 200);

        Assert.True(newX > 0, "x must move toward positive target");
        Assert.True(newY > 0, "y must move toward positive target");
        Assert.True(newX < 100, "x must not overshoot in first frame");
        Assert.True(newY < 200, "y must not overshoot in first frame");
    }

    [Fact]
    public void Step_FromRestAtPositive_MovesTowardNegativeTarget()
    {
        var spring = new SpringSimulator();

        var (newX, newY) = spring.Step(currentX: 100, currentY: 50, targetX: 0, targetY: 0);

        Assert.True(newX < 100, "x must move toward zero");
        Assert.True(newY < 50, "y must move toward zero");
    }

    [Fact]
    public void Step_AtEquilibrium_WithZeroVelocity_DoesNotMove()
    {
        var spring = new SpringSimulator { VelocityX = 0, VelocityY = 0 };

        var (newX, newY) = spring.Step(currentX: 50, currentY: 75, targetX: 50, targetY: 75);

        Assert.Equal(50.0, newX, precision: 10);
        Assert.Equal(75.0, newY, precision: 10);
        Assert.Equal(0.0, spring.VelocityX, precision: 10);
        Assert.Equal(0.0, spring.VelocityY, precision: 10);
    }

    [Fact]
    public void Step_XAndYAxesAreIndependent()
    {
        var springA = new SpringSimulator();
        var (xA, _) = springA.Step(0, 0, 100, 0);

        var springB = new SpringSimulator();
        var (_, yB) = springB.Step(0, 0, 0, 100);

        // Symmetric spring: 100px in X vs 100px in Y should yield equal displacement
        Assert.Equal(xA, yB, precision: 10);
    }

    [Fact]
    public void Step_UpdatesVelocityAfterFirstStep()
    {
        var spring = new SpringSimulator();

        spring.Step(0, 0, 100, 100);

        Assert.True(spring.VelocityX > 0, "velocity must be positive toward positive target");
        Assert.True(spring.VelocityY > 0, "velocity must be positive toward positive target");
    }

    [Fact]
    public void Step_HighVelocityAwayFromTarget_IsDampedByB()
    {
        // Large velocity away from the target; damping should reduce it
        var spring = new SpringSimulator { VelocityX = 1000, VelocityY = 0 };

        spring.Step(currentX: 0, currentY: 0, targetX: 0, targetY: 0);

        // Net force: spring=0, damping=-1000*B → velocity decreases significantly
        Assert.True(spring.VelocityX < 1000, "damping must reduce velocity");
    }

    [Fact]
    public void Step_AfterManyFrames_ConvergesOnTarget()
    {
        var spring = new SpringSimulator();
        double x = 0, y = 0;
        const double targetX = 500, targetY = 300;

        for (int i = 0; i < 300; i++) // 5 seconds at 60fps — well past settling time
            (x, y) = spring.Step(x, y, targetX, targetY);

        // Damping fraction 0.6 is underdamped but settles within ~1dp after 5s
        Assert.Equal(targetX, x, precision: 1);
        Assert.Equal(targetY, y, precision: 1);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ZerosVelocityOnBothAxes()
    {
        var spring = new SpringSimulator { VelocityX = 123.4, VelocityY = -567.8 };

        spring.Reset();

        Assert.Equal(0.0, spring.VelocityX);
        Assert.Equal(0.0, spring.VelocityY);
    }

    [Fact]
    public void Reset_AfterActiveMotion_ClearsResidualVelocity()
    {
        var spring = new SpringSimulator();
        double x = 0, y = 0;

        for (int i = 0; i < 10; i++)
            (x, y) = spring.Step(x, y, 100, 100);

        Assert.True(spring.VelocityX != 0 || spring.VelocityY != 0, "velocity should be non-zero after motion");

        spring.Reset();

        Assert.Equal(0.0, spring.VelocityX);
        Assert.Equal(0.0, spring.VelocityY);
    }
}
