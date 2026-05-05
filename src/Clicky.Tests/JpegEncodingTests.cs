using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace Clicky.Tests;

public class JpegEncodingTests
{
    [Fact]
    public void JpegEncoding_SmallBitmap_ProducesValidJpeg()
    {
        // Arrange
        using var bmp = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Blue);

        // Act
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Jpeg);
        var bytes = ms.ToArray();

        // Assert — non-empty and starts with JPEG SOI magic bytes FF D8 FF
        Assert.True(bytes.Length > 0);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        Assert.Equal(0xFF, bytes[2]);
    }

    [Fact]
    public void JpegEncoding_DifferentColors_ProducesDistinctOutput()
    {
        // Arrange
        using var redBmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var greenBmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(redBmp))
            g.Clear(Color.Red);
        using (var g = Graphics.FromImage(greenBmp))
            g.Clear(Color.Green);

        // Act
        using var redMs = new MemoryStream();
        using var greenMs = new MemoryStream();
        redBmp.Save(redMs, ImageFormat.Jpeg);
        greenBmp.Save(greenMs, ImageFormat.Jpeg);

        // Assert — same dimensions but different color content → different bytes
        Assert.NotEqual(redMs.ToArray(), greenMs.ToArray());
    }
}
