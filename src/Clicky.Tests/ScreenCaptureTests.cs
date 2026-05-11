using Clicky.Core;
using Xunit;

namespace Clicky.Tests;

public class ScreenCaptureTests
{
    [Fact]
    public void ScreenCapture_StoresAllProperties()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF };
        var bounds = new MonitorBounds(10, 20, 1920, 1080);
        var capture = new ScreenCapture(jpeg, 1920, 1080, bounds);

        Assert.Same(jpeg, capture.Jpeg);
        Assert.Equal(1920, capture.Width);
        Assert.Equal(1080, capture.Height);
        Assert.Equal(bounds, capture.MonitorPhysBounds);
    }
}
