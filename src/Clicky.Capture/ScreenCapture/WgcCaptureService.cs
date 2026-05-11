using System.Drawing;
using System.Drawing.Imaging;
using Clicky.Core;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using CoreScreenCapture = Clicky.Core.ScreenCapture;

namespace Clicky.Capture.ScreenCapture;

public sealed class WgcCaptureService : IScreenCaptureService
{
    /// <inheritdoc/>
    public Task<CoreScreenCapture> CaptureAsync(CancellationToken cancellationToken = default)
        => Task.Run(CaptureJpeg, cancellationToken);

    private static CoreScreenCapture CaptureJpeg()
    {
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Jpeg);

        var bounds = new MonitorBounds(0, 0, width, height);
        return new CoreScreenCapture(ms.ToArray(), width, height, bounds);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
