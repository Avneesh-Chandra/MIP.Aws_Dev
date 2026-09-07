using MIP.Aws.Infrastructure.Operator;
using SkiaSharp;

namespace MIP.Aws.Tests;

public sealed class PdfDownloadMailHeroComposerTests
{
    [Fact]
    public void ComposeJpegFromPages_fans_covers_into_jpeg()
    {
        using var left = CreateSolidBitmap(120, 180, SKColors.OrangeRed);
        using var right = CreateSolidBitmap(120, 180, SKColors.SteelBlue);

        var jpeg = PdfDownloadMailHeroComposer.ComposeJpegFromPages([left, right]);

        Assert.NotNull(jpeg);
        Assert.True(jpeg!.Length > 500);
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]); // JPEG SOI marker
    }

    [Fact]
    public void ComposeJpegFromPages_uses_sample_light_background()
    {
        using var page = CreateSolidBitmap(100, 140, SKColors.Navy);
        var jpeg = PdfDownloadMailHeroComposer.ComposeJpegFromPages([page]);
        Assert.NotNull(jpeg);

        using var decoded = SKBitmap.Decode(jpeg);
        Assert.NotNull(decoded);
        // RichBlack #332E2D near top-left of the title scrim.
        var pixel = decoded!.GetPixel(8, 8);
        Assert.InRange(pixel.Red, 40, 70);
        Assert.InRange(pixel.Green, 35, 65);
        Assert.InRange(pixel.Blue, 35, 65);
    }

    private static SKBitmap CreateSolidBitmap(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        return bitmap;
    }
}
