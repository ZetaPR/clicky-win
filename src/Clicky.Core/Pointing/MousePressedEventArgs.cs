namespace Clicky.Core;

/// <summary>Carries screen coordinates and button code from the global mouse hook.</summary>
public sealed class MousePressedEventArgs(int x, int y, string buttonCode) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public string ButtonCode { get; } = buttonCode;
}
