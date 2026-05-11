using Clicky.Core;
using Clicky.Services;
using NSubstitute;
using Xunit;

namespace Clicky.Tests;

public class StepClickWatcherTests
{
    private static IPushToTalkHook MakePtt() => Substitute.For<IPushToTalkHook>();

    [Fact]
    public void ClickWithinRadius_WhenArmed_FiresClickConfirmed()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(520, 310, "Button1"));

        Assert.True(fired);
    }

    [Fact]
    public void ClickOutsideRadius_WhenArmed_DoesNotFire()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(700, 500, "Button1"));

        Assert.False(fired);
    }

    [Fact]
    public void Click_WhenDisarmed_DoesNotFire()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);
        watcher.Disarm();

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(510, 305, "Button1"));

        Assert.False(fired);
    }

    [Fact]
    public void ClickExactlyAtRadius_Fires()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 100, targetY: 100);

        // sqrt(42^2 + 42^2) ≈ 59.4 — within 60px radius
        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(142, 142, "Button1"));

        Assert.True(fired);
    }
}
