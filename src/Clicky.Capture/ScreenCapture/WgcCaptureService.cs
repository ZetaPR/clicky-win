using System.Drawing;
using System.Drawing.Imaging;
using Clicky.Core;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicky.Capture.ScreenCapture;

/// <summary>
/// Phase 1 walking-skeleton screen capture using GDI CopyFromScreen.
/// Will be replaced in Phase 4 with true Windows.Graphics.Capture (WGC)
/// once full D3D11 interop is wired up for multi-monitor + DPI support.
/// Limitation: CopyFromScreen may miss hardware-accelerated content
/// (Chromium, WPF, exclusive-fullscreen games).
/// </summary>
public sealed class WgcCaptureService : IScreenCaptureService
{
    /// <inheritdoc/>
    public Task<byte[]> CapturePrimaryMonitorAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CaptureJpeg(), cancellationToken);
    }

    private static byte[] CaptureJpeg()
    {
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
