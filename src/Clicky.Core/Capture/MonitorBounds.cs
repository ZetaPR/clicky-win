namespace Clicky.Core;

/// <summary>Physical pixel bounds of a monitor in virtual-screen coordinates.</summary>
public readonly record struct MonitorBounds(int X, int Y, int Width, int Height);
