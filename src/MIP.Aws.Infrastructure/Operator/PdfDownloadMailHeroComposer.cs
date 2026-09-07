using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class PdfDownloadMailHeroComposer(
    IFileStorageService fileStorage,
    ILogger<PdfDownloadMailHeroComposer> logger) : IPdfDownloadMailHeroComposer
{
    private const int MaxCovers = 6;
    private const int CanvasWidth = 680;
    private const int CanvasHeight = 440;
    private const int CoverHeight = 220;
    private const int MaxPdfBytes = 80 * 1024 * 1024;
    private const int RenderDpi = 72;
    private const int JpegQuality = 88;

    // Previous attractive Modern palette (RichBlack + Copper accents).
    private static readonly SKColor CanvasBackground = new(0x33, 0x2E, 0x2D);
    private static readonly SKColor TitleCopper = new(0xAF, 0x78, 0x46);
    private static readonly SKColor TaglineLeather = new(0xC8, 0xC1, 0xB1);

    public async Task<byte[]?> TryComposeJpegAsync(
        IReadOnlyList<string> pdfRelativeBlobKeys,
        CancellationToken cancellationToken = default)
    {
        if (pdfRelativeBlobKeys.Count == 0)
        {
            return null;
        }

        var keys = pdfRelativeBlobKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Take(MaxCovers)
            .ToList();
        if (keys.Count == 0)
        {
            return null;
        }

        var pageBitmaps = new List<SKBitmap>(keys.Count);
        try
        {
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pdfBytes = await fileStorage.ReadAsync(key.Trim(), cancellationToken).ConfigureAwait(false);
                if (pdfBytes is null || pdfBytes.Length == 0)
                {
                    logger.LogWarning("PDF mail hero skipped missing blob {BlobKey}.", key);
                    continue;
                }

                if (pdfBytes.Length > MaxPdfBytes)
                {
                    logger.LogWarning(
                        "PDF mail hero skipped oversized blob {BlobKey} ({SizeBytes} bytes).",
                        key,
                        pdfBytes.Length);
                    continue;
                }

                try
                {
                    var page = await Task.Run(
                            () => Conversion.ToImage(
                                pdfBytes,
                                page: 0,
                                options: new RenderOptions(Dpi: RenderDpi)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (page is not null && page.Width > 0 && page.Height > 0)
                    {
                        pageBitmaps.Add(page);
                    }
                    else
                    {
                        page?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "PDF mail hero failed to render first page for {BlobKey}.", key);
                }
            }

            if (pageBitmaps.Count == 0)
            {
                return null;
            }

            return await Task.Run(
                    () => ComposeJpegFromPages(pageBitmaps),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF mail hero composite generation failed.");
            return null;
        }
        finally
        {
            foreach (var bitmap in pageBitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    /// <summary>Visible for unit tests — fans page bitmaps onto the brand canvas with title copy on top.</summary>
    internal static byte[]? ComposeJpegFromPages(IReadOnlyList<SKBitmap> pages)
    {
        if (pages.Count == 0)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(CanvasWidth, CanvasHeight));
        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;
        canvas.Clear(CanvasBackground);

        var covers = new List<SKBitmap>(pages.Count);
        try
        {
            foreach (var page in pages.Take(MaxCovers))
            {
                var scaled = ScaleToHeight(page, CoverHeight);
                if (scaled is not null)
                {
                    covers.Add(scaled);
                }
            }

            if (covers.Count == 0)
            {
                return null;
            }

            var angles = BuildAngles(covers.Count);
            // Papers sit in the lower band so title space above stays clear.
            var centerX = CanvasWidth / 2f;
            var centerY = CanvasHeight * 0.68f;
            var spacing = covers.Count == 1
                ? 0f
                : Math.Min(110f, (CanvasWidth - 140f) / (covers.Count - 1));

            for (var i = 0; i < covers.Count; i++)
            {
                var cover = covers[i];
                var offsetIndex = i - (covers.Count - 1) / 2f;
                DrawCover(canvas, cover, centerX + offsetIndex * spacing, centerY, angles[i]);
            }

            // Draw title last so newspapers never cover the copy.
            DrawBrandCopy(canvas);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            return data?.ToArray();
        }
        finally
        {
            foreach (var cover in covers)
            {
                cover.Dispose();
            }
        }
    }

    private static void DrawBrandCopy(SKCanvas canvas)
    {
        using var titleTypeface = SKTypeface.FromFamilyName(
            "Georgia",
            SKFontStyleWeight.Bold,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        using var tagTypeface = SKTypeface.FromFamilyName(
            "Arial",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        using var titlePaint = new SKPaint { Color = TitleCopper, IsAntialias = true };
        using var tagPaint = new SKPaint { Color = TaglineLeather, IsAntialias = true };

        // Soft scrim behind text for readability over any cover overlap.
        using (var scrim = new SKPaint { Color = new SKColor(0x33, 0x2E, 0x2D, 210), IsAntialias = true })
        {
            canvas.DrawRect(SKRect.Create(0, 0, CanvasWidth, 168), scrim);
        }

        const float left = 36f;
        using (var dailyFont = new SKFont(titleTypeface, 32f))
        {
            canvas.DrawText("The Daily", left, 62f, SKTextAlign.Left, dailyFont, titlePaint);
        }

        using (var newspaperFont = new SKFont(titleTypeface, 48f))
        {
            canvas.DrawText("Newspaper", left, 112f, SKTextAlign.Left, newspaperFont, titlePaint);
        }

        using (var tagFont = new SKFont(tagTypeface, 16f))
        {
            canvas.DrawText("Stay ahead of the story!", left, 146f, SKTextAlign.Left, tagFont, tagPaint);
        }
    }

    private static float[] BuildAngles(int count)
    {
        if (count <= 1)
        {
            return [-4f];
        }

        const float min = -12f;
        const float max = 12f;
        var angles = new float[count];
        for (var i = 0; i < count; i++)
        {
            angles[i] = min + (max - min) * i / (count - 1);
        }

        return angles;
    }

    private static SKBitmap? ScaleToHeight(SKBitmap source, int targetHeight)
    {
        if (source.Height <= 0 || source.Width <= 0)
        {
            return null;
        }

        var scale = targetHeight / (float)source.Height;
        var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    private static void DrawCover(SKCanvas canvas, SKBitmap cover, float centerX, float centerY, float angleDegrees)
    {
        var halfW = cover.Width / 2f;
        var halfH = cover.Height / 2f;
        var dest = SKRect.Create(-halfW, -halfH, cover.Width, cover.Height);

        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees(angleDegrees);

        using (var shadowPaint = new SKPaint())
        {
            shadowPaint.IsAntialias = true;
            shadowPaint.ImageFilter = SKImageFilter.CreateDropShadow(
                5,
                8,
                10,
                10,
                new SKColor(0, 0, 0, 170));
            canvas.DrawRect(dest, shadowPaint);
        }

        using (var imagePaint = new SKPaint())
        {
            imagePaint.IsAntialias = true;
            canvas.DrawBitmap(
                cover,
                dest,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                imagePaint);
        }

        using (var borderPaint = new SKPaint())
        {
            borderPaint.IsAntialias = true;
            borderPaint.Style = SKPaintStyle.Stroke;
            borderPaint.Color = new SKColor(255, 255, 255, 210);
            borderPaint.StrokeWidth = 2f;
            canvas.DrawRect(dest, borderPaint);
        }

        canvas.Restore();
    }
}
