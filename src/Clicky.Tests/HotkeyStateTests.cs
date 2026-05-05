using Clicky.Capture.Hotkeys;
using Xunit;

namespace Clicky.Tests;

public class HotkeyStateTests
{
    // ── OnKeyDown ──────────────────────────────────────────────────────────

    [Fact]
    public void OnKeyDown_AllThreeHeld_StartsRecording()
    {
        // Arrange
        var state = new HotkeyState();

        // Act
        state.OnKeyDown(ctrl: true, win: false, space: false);
        state.OnKeyDown(ctrl: false, win: true, space: false);
        var started = state.OnKeyDown(ctrl: false, win: false, space: true);

        // Assert
        Assert.True(started);
        Assert.True(state.IsRecording);
    }

    [Fact]
    public void OnKeyDown_PartialHold_CtrlAndWinOnly_DoesNotStart()
    {
        // Arrange
        var state = new HotkeyState();

        // Act
        state.OnKeyDown(ctrl: true, win: false, space: false);
        var started = state.OnKeyDown(ctrl: false, win: true, space: false);

        // Assert
        Assert.False(started);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyDown_PartialHold_SpaceOnly_DoesNotStart()
    {
        // Arrange
        var state = new HotkeyState();

        // Act
        var started = state.OnKeyDown(ctrl: false, win: false, space: true);

        // Assert
        Assert.False(started);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyDown_WhenAlreadyRecording_DoesNotDoubleStart()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: true, space: true);  // first start

        // Act — redundant key-repeat event for Ctrl while all three are still held
        var startedAgain = state.OnKeyDown(ctrl: true, win: false, space: false);

        // Assert
        Assert.False(startedAgain);
        Assert.True(state.IsRecording);
    }

    // ── OnKeyUp ───────────────────────────────────────────────────────────

    [Fact]
    public void OnKeyUp_ReleaseSpaceWhileRecording_StopsRecording()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: true, space: true);

        // Act
        var stopped = state.OnKeyUp(ctrl: false, win: false, space: true);

        // Assert
        Assert.True(stopped);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyUp_ReleaseCtrlWhileRecording_StopsRecording()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: true, space: true);

        // Act
        var stopped = state.OnKeyUp(ctrl: true, win: false, space: false);

        // Assert
        Assert.True(stopped);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyUp_ReleaseWinWhileRecording_StopsRecording()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: true, space: true);

        // Act
        var stopped = state.OnKeyUp(ctrl: false, win: true, space: false);

        // Assert
        Assert.True(stopped);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyUp_WhenNotRecording_DoesNotFireStop()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: false, space: false);  // partial — not recording

        // Act
        var stopped = state.OnKeyUp(ctrl: true, win: false, space: false);

        // Assert
        Assert.False(stopped);
        Assert.False(state.IsRecording);
    }

    [Fact]
    public void OnKeyUp_UnrelatedKey_DoesNotStopRecording()
    {
        // Arrange
        var state = new HotkeyState();
        state.OnKeyDown(ctrl: true, win: true, space: true);

        // Act — OnKeyUp called with no modifiers (an unrelated key released)
        var stopped = state.OnKeyUp(ctrl: false, win: false, space: false);

        // Assert
        Assert.False(stopped);
        Assert.True(state.IsRecording);
    }

    // ── Round-trip ─────────────────────────────────────────────────────────

    [Fact]
    public void FullCycle_AfterStop_RequiresFullComboToRestart()
    {
        // Arrange
        var state = new HotkeyState();

        // Press all three to start
        state.OnKeyDown(ctrl: true, win: false, space: false);
        state.OnKeyDown(ctrl: false, win: true, space: false);
        state.OnKeyDown(ctrl: false, win: false, space: true);
        Assert.True(state.IsRecording);

        // Release space to stop
        state.OnKeyUp(ctrl: false, win: false, space: true);
        Assert.False(state.IsRecording);

        // Release all remaining keys
        state.OnKeyUp(ctrl: true, win: false, space: false);
        state.OnKeyUp(ctrl: false, win: true, space: false);

        // Space alone should NOT restart (ctrl and win no longer held)
        state.OnKeyDown(ctrl: false, win: false, space: true);
        Assert.False(state.IsRecording);

        // Full combo should restart
        state.OnKeyDown(ctrl: true, win: false, space: false);
        state.OnKeyDown(ctrl: false, win: true, space: false);
        Assert.True(state.IsRecording);
    }
}
