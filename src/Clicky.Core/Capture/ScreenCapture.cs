namespace Clicky.Core;

public sealed record ScreenCapture(
    byte[] Jpeg,
    int Width,
    int Height,
    MonitorBounds MonitorPhysBounds);
