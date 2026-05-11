namespace Clicky.Core;

/// <summary>Carries a mouse button code ("MouseButton1", "MouseButton3", etc.) from the global hook.</summary>
public sealed class HookMouseEventArgs(string buttonCode) : EventArgs
{
    public string ButtonCode { get; } = buttonCode;
}
