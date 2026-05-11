using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepPlanStoreTests
{
    private static ScreenCapture MakeCapture() =>
        new(Array.Empty<byte>(), 100, 100, new MonitorBounds(0, 0, 100, 100));

    [Fact]
    public void StepPlanStore_InitiallyNotActive()
    {
        var store = new StepPlanStore();
        Assert.False(store.IsActive);
        Assert.Null(store.CurrentStep);
    }

    [Fact]
    public void Load_SetsActiveAndStoresCapture()
    {
        var store = new StepPlanStore();
        var capture = MakeCapture();
        store.Load(capture, nint.Zero);
        Assert.True(store.IsActive);
        Assert.Same(capture, store.OriginalCapture);
    }

    [Fact]
    public void AddStep_BecomesCurrentStep_WhenFirstAdded()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "do this", X = 10, Y = 20 });
        Assert.NotNull(store.CurrentStep);
        Assert.Equal(1, store.CurrentStep!.Number);
    }

    [Fact]
    public void AdvanceTo_UpdatesCurrentStep()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "step one", X = 10, Y = 20 });
        store.AddStep(new Step { Number = 2, Text = "step two" });
        store.AdvanceTo(1, nextX: 50, nextY: 60, nextLabel: null);
        Assert.Equal(2, store.CurrentStep!.Number);
        Assert.Equal(50, store.CurrentStep.X);
    }

    [Fact]
    public void Clear_ResetsStore()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "do this" });
        store.Clear();
        Assert.False(store.IsActive);
        Assert.Null(store.CurrentStep);
    }

    [Fact]
    public void AppendHistory_GrowsHistoryList()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AppendHistory(new LlmMessage("assistant", "step one text"));
        store.AppendHistory(new LlmMessage("user", "clicked"));
        Assert.Equal(2, store.History.Count);
    }

    [Fact]
    public async Task TimedOut_FiresAfterTimeout()
    {
        var store = new StepPlanStore(timeoutSeconds: 0.15);
        bool fired = false;
        store.TimedOut += (_, _) => fired = true;
        store.Load(MakeCapture(), nint.Zero);
        await Task.Delay(400);
        Assert.True(fired);
        Assert.False(store.IsActive);
    }
}
