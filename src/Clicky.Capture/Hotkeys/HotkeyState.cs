namespace Clicky.Capture.Hotkeys;

/// <summary>Tracks the Ctrl+Win+Space key-combination state and drives recording start/stop transitions.</summary>
internal sealed class HotkeyState
{
    public bool CtrlHeld { get; private set; }
    public bool WinHeld { get; private set; }
    public bool SpaceHeld { get; private set; }
    public bool IsRecording { get; private set; }

    /// <summary>
    /// Applies a key-down event for the given modifier flags.
    /// Returns <see langword="true"/> when the combo just became fully held and recording should start.
    /// </summary>
    public bool OnKeyDown(bool ctrl, bool win, bool space)
    {
        if (ctrl) CtrlHeld = true;
        if (win) WinHeld = true;
        if (space) SpaceHeld = true;

        if (CtrlHeld && WinHeld && SpaceHeld && !IsRecording)
        {
            IsRecording = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies a key-up event for the given modifier flags.
    /// Returns <see langword="true"/> when the combo broke while recording and recording should stop.
    /// </summary>
    public bool OnKeyUp(bool ctrl, bool win, bool space)
    {
        if (ctrl) CtrlHeld = false;
        if (win) WinHeld = false;
        if (space) SpaceHeld = false;

        if (IsRecording && (!CtrlHeld || !WinHeld || !SpaceHeld))
        {
            IsRecording = false;
            return true;
        }

        return false;
    }
}
