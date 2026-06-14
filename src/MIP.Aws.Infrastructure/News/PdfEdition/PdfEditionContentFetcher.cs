using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Fetches PDF bytes via HTTP, with optional Playwright fallback for bot-protected publishers.
/// </summary>
public sealed class PdfEditionContentFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<PdfEditionContentFetcher> logger)
{
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    public static bool IsPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= PdfMagic.Length && bytes[..PdfMagic.Length].SequenceEqual(PdfMagic);

    public Task<byte[]?> FetchAsync(
        Uri url,
        bool useHeadlessBrowser,
        Uri? warmUpUrl,
        CancellationToken cancellationToken,
        IProgress<int>? byteDownloadProgress = null) =>
        FetchAsync(url, useHeadlessBrowser, warmUpUrl, source: null, cancellationToken, byteDownloadProgress);

    public Task<byte[]?> FetchAsync(
        Uri url,
        bool useHeadlessBrowser,
        Uri? warmUpUrl,
        NewsSource? source,
        CancellationToken cancellationToken,
        IProgress<int>? byteDownloadProgress = null) =>
        FetchAsync(url, useHeadlessBrowser, warmUpUrl, source, cancellationToken, byteDownloadProgress is null
            ? null
            : p => byteDownloadProgress.Report(p));

    public async Task<byte[]?> FetchAsync(
        Uri url,
        bool useHeadlessBrowser,
        Uri? warmUpUrl,
        NewsSource? source,
        CancellationToken cancellationToken,
        Action<int>? reportByteProgress)
    {
        var httpBytes = await TryHttpGetAsync(url, reportByteProgress, cancellationToken).ConfigureAwait(false);
        if (httpBytes is not null && IsPdf(httpBytes))
        {
            reportByteProgress?.Invoke(100);
            return httpBytes;
        }

        if (!useHeadlessBrowser)
        {
            return httpBytes;
        }

        logger.LogInformation("HTTP fetch for {Url} was not a PDF; retrying with Playwright.", url);
        reportByteProgress?.Invoke(10);
        var browserBytes = await FetchViaPlaywrightAsync(url, warmUpUrl, source, cancellationToken).ConfigureAwait(false);
        reportByteProgress?.Invoke(browserBytes is not null && IsPdf(browserBytes) ? 100 : 0);
        return browserBytes ?? httpBytes;
    }

    private async Task<byte[]?> TryHttpGetAsync(
        Uri url,
        Action<int>? reportByteProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(PdfEditionContentFetcher));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                ms.Write(buffer, 0, read);
                readTotal += read;
                if (reportByteProgress is not null && total is > 0)
                {
                    var pct = (int)Math.Clamp(readTotal * 100 / total.Value, 0, 100);
                    reportByteProgress(pct);
                }
            }

            return ms.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HTTP GET failed for {Url}", url);
            return null;
        }
    }

    private async Task<byte[]?> FetchViaPlaywrightAsync(
        Uri pdfUrl,
        Uri? warmUpUrl,
        NewsSource? source,
        CancellationToken cancellationToken)
    {
        if (source is not null
            && (pdfUrl.Host.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase)
                || AawsatFullPublicationPdf.UsesClickPath(source)))
        {
            var fromClickPath = await AawsatFullPublicationPdf.TryDownloadBytesWithFallbacksAsync(
                    pdfUrl,
                    source,
                    warmUpUrl,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
            if (fromClickPath is not null && IsPdf(fromClickPath))
            {
                logger.LogInformation("Fetched Aawsat Full Publication PDF via click path for {Url}", pdfUrl);
                return fromClickPath;
            }
        }

        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true,
                Locale = "en-US"
            }).ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(120_000);

            if (warmUpUrl is not null)
            {
                await page.GotoAsync(
                    warmUpUrl.ToString(),
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 90_000 }).ConfigureAwait(false);
            }

            var response = await page.GotoAsync(
                pdfUrl.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 }).ConfigureAwait(false);

            if (response is null || !response.Ok)
            {
                logger.LogWarning("Playwright navigation to {Url} returned {Status}", pdfUrl, response?.Status);
                return null;
            }

            var body = await response.BodyAsync().ConfigureAwait(false);
            if (IsPdf(body))
            {
                return body;
            }

            logger.LogWarning(
                "Playwright fetch for {Url} returned non-PDF content ({Length} bytes, content-type {ContentType}).",
                pdfUrl,
                body.Length,
                response.Headers.TryGetValue("content-type", out var ct) ? ct : "unknown");

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Playwright PDF fetch failed for {Url}", pdfUrl);
            return null;
        }
    }
}
