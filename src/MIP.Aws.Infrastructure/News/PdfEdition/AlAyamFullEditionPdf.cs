using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.Browser;
using MIP.Aws.Infrastructure.News;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Bahrain Al Ayam: HTTP epaper HTML → INAF PDF, then Playwright click path as fallback.
/// </summary>
public static class AlAyamFullEditionPdf
{
    public const string EpaperUrl = "https://www.alayam.com/epaper";

    public const string AllPagesLinkSelector =
        "a#aPDFdownloadAllPages, a[href*='i.alayam.com'][href*='INAF_'][href$='.pdf']";

    private static readonly Regex AllPagesPdfHrefRegex = new(
        @"id=""aPDFdownloadAllPages""[^>]*href=""(?<url>https://i\.alayam\.com/[^""]+\.pdf)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InafPdfUrlRegex = new(
        @"https://i\.alayam\.com/ayamnewsa/upload/issue/\d+/\d+/PDF/INAF_[^""'\s<>]+\.pdf",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool UsesClickPath(NewsSource source) =>
        string.Equals(source.ConnectorKey, AlAyamEditionDiscovery.Key, StringComparison.OrdinalIgnoreCase);

    public static bool IsDirectPdfUrl(Uri url) =>
        url.Host.Contains("i.alayam.com", StringComparison.OrdinalIgnoreCase)
        && url.AbsolutePath.Contains("/PDF/INAF_", StringComparison.OrdinalIgnoreCase)
        && url.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public static int ResolveTimeoutMs(NewsSource? source) =>
        source is null ? 120_000 : Math.Clamp(source.DownloadWaitTimeoutSeconds, 60, 600) * 1000;

    public static Uri ResolveEpaperUri(NewsSource source, Uri? fallback = null)
    {
        foreach (var raw in new[] { source.PdfDiscoveryPageUrl, source.EditionUrl, source.BaseUrl })
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)
                && uri.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }
        }

        if (fallback is not null
            && fallback.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
            && fallback.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        return new Uri(EpaperUrl);
    }

    public static Uri? ExtractPdfUrlFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = AllPagesPdfHrefRegex.Match(html);
        if (match.Success
            && Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var fromAnchor))
        {
            return fromAnchor;
        }

        var inaf = InafPdfUrlRegex.Match(html);
        return inaf.Success && Uri.TryCreate(inaf.Value, UriKind.Absolute, out var fromInaf)
            ? fromInaf
            : null;
    }

    public static Task<AlAyamFullEditionResult?> TryDiscoverAsync(
        Uri startPageUrl,
        NewsSource source,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryDiscoverAsync(startPageUrl, source, httpClientFactory: null, logger, cancellationToken);

    public static async Task<AlAyamFullEditionResult?> TryDiscoverAsync(
        Uri startPageUrl,
        NewsSource source,
        IHttpClientFactory? httpClientFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (httpClientFactory is not null)
        {
            var knownPdf = IsDirectPdfUrl(startPageUrl) ? startPageUrl : null;
            var httpUrl = knownPdf
                          ?? await AlAyamHttpPdfPath.TryDiscoverPdfUrlAsync(
                              httpClientFactory,
                              source,
                              logger,
                              cancellationToken)
                              .ConfigureAwait(false);
            if (httpUrl is not null)
            {
                return new AlAyamFullEditionResult(httpUrl, null);
            }
        }

        return await RunClickPathAsync(startPageUrl, source, logger, downloadBytes: false, cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task<byte[]?> TryDownloadBytesAsync(
        Uri startPageUrl,
        NewsSource? source,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryDownloadBytesAsync(startPageUrl, source, httpClientFactory: null, logger, cancellationToken);

    public static async Task<byte[]?> TryDownloadBytesAsync(
        Uri startPageUrl,
        NewsSource? source,
        IHttpClientFactory? httpClientFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (httpClientFactory is not null && source is not null)
        {
            var knownPdf = IsDirectPdfUrl(startPageUrl) ? startPageUrl : null;
            var fromHttp = await AlAyamHttpPdfPath.TryDownloadBytesAsync(
                    httpClientFactory,
                    source,
                    knownPdf,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
            if (fromHttp is not null && PdfEditionContentFetcher.IsPdf(fromHttp))
            {
                return fromHttp;
            }
        }

        var result = await RunClickPathAsync(startPageUrl, source, logger, downloadBytes: true, cancellationToken)
            .ConfigureAwait(false);
        return result?.PdfBytes;
    }

    public static Task<byte[]?> TryDownloadBytesWithFallbacksAsync(
        Uri candidateOrPage,
        NewsSource source,
        Uri? warmUpUrl,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryDownloadBytesWithFallbacksAsync(
            candidateOrPage,
            source,
            warmUpUrl,
            httpClientFactory: null,
            logger,
            cancellationToken);

    public static async Task<byte[]?> TryDownloadBytesWithFallbacksAsync(
        Uri candidateOrPage,
        NewsSource source,
        Uri? warmUpUrl,
        IHttpClientFactory? httpClientFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (httpClientFactory is not null)
        {
            var knownPdf = IsDirectPdfUrl(candidateOrPage) ? candidateOrPage : null;
            var fromHttp = await AlAyamHttpPdfPath.TryDownloadBytesAsync(
                    httpClientFactory,
                    source,
                    knownPdf,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
            if (fromHttp is not null && fromHttp.Length > 0 && PdfEditionContentFetcher.IsPdf(fromHttp))
            {
                return fromHttp;
            }
        }

        var starts = new List<Uri>();
        if (IsDirectPdfUrl(candidateOrPage))
        {
            starts.Add(candidateOrPage);
        }

        if (warmUpUrl is not null && warmUpUrl.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
        {
            starts.Add(warmUpUrl);
        }

        if (!string.IsNullOrWhiteSpace(source.PdfDiscoveryPageUrl)
            && Uri.TryCreate(source.PdfDiscoveryPageUrl.Trim(), UriKind.Absolute, out var discoveryPage))
        {
            starts.Add(discoveryPage);
        }

        starts.Add(new Uri(EpaperUrl));

        foreach (var start in starts
                     .Where(u => u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
                     .DistinctBy(u => u.ToString().TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
        {
            var bytes = await TryDownloadBytesAsync(start, source, httpClientFactory, logger, cancellationToken)
                .ConfigureAwait(false);
            if (bytes is not null && bytes.Length > 0 && PdfEditionContentFetcher.IsPdf(bytes))
            {
                return bytes;
            }
        }

        return null;
    }

    private static async Task<AlAyamFullEditionResult?> RunClickPathAsync(
        Uri startPageUrl,
        NewsSource? source,
        ILogger logger,
        bool downloadBytes,
        CancellationToken cancellationToken)
    {
        var epaperUri = ResolveEpaperUri(startPageUrl, source);
        var timeoutMs = ResolveTimeoutMs(source);
        var userAgent = ResolveUserAgent();

        try
        {
            using var playwright = await PlaywrightBrowserLaunch.CreatePlaywrightAsync().ConfigureAwait(false);
            await using var browser = await PlaywrightBrowserLaunch.LaunchChromiumAsync(playwright).ConfigureAwait(false);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true,
                Locale = "ar-BH",
                TimezoneId = "Asia/Bahrain",
                UserAgent = userAgent,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "ar-BH,ar;q=0.9,en;q=0.8"
                }
            }).ConfigureAwait(false);

            await ApplyStealthScriptsAsync(context).ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(timeoutMs);

            await page.GotoAsync(
                epaperUri.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutMs })
                .ConfigureAwait(false);

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // continue polling
            }

            if (source is not null)
            {
                var editionCheck = await SourcePageEditionDatePageVerifier.VerifyAsync(
                    page,
                    source,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    cancellationToken).ConfigureAwait(false);
                if (editionCheck.BlocksDownload)
                {
                    logger.LogWarning(
                        "Al Ayam edition date verification failed for {Url}: {Message}",
                        epaperUri,
                        editionCheck.FailureMessage);
                    return null;
                }
            }

            var resolve = await ResolvePdfUrlAsync(page, epaperUri, timeoutMs, logger, cancellationToken)
                .ConfigureAwait(false);
            if (resolve.AccessBlocked)
            {
                logger.LogWarning(
                    "Al Ayam e-paper blocked by publisher bot protection on {Url} (datacenter egress; HTTP path already attempted).",
                    epaperUri);
                return new AlAyamFullEditionResult(null, null, AccessBlocked: true);
            }

            if (resolve.PdfUrl is null)
            {
                logger.LogWarning("Al Ayam click path: all-pages PDF link not found from {Url}", epaperUri);
                return null;
            }

            logger.LogInformation("Al Ayam all-pages PDF resolved -> {Url}", resolve.PdfUrl);

            if (!downloadBytes)
            {
                return new AlAyamFullEditionResult(resolve.PdfUrl, null);
            }

            var bytes = await FetchPdfBytesViaPlaywrightRequestAsync(
                    context,
                    resolve.PdfUrl,
                    epaperUri,
                    timeoutMs,
                    cancellationToken)
                .ConfigureAwait(false);
            if (bytes is null || !PdfEditionContentFetcher.IsPdf(bytes))
            {
                if (await TryClickAllPagesLinkAsync(page, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        var download = await page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = timeoutMs })
                            .ConfigureAwait(false);
                        var path = await download.PathAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Al Ayam Playwright download event failed.");
                    }
                }
            }

            if (bytes is null || bytes.Length == 0 || !PdfEditionContentFetcher.IsPdf(bytes))
            {
                logger.LogWarning("Al Ayam click path: PDF fetch failed for {Url}", resolve.PdfUrl);
                return null;
            }

            logger.LogInformation("Al Ayam PDF downloaded ({Bytes} bytes) from {Url}", bytes.Length, resolve.PdfUrl);
            return new AlAyamFullEditionResult(resolve.PdfUrl, bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Al Ayam all-pages click path failed from {Url}", epaperUri);
            return null;
        }
    }

    private static string ResolveUserAgent() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";

    private static async Task ApplyStealthScriptsAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(
            """
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            Object.defineProperty(navigator, 'languages', { get: () => ['ar-BH', 'ar', 'en'] });
            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
            window.chrome = { runtime: {} };
            """).ConfigureAwait(false);
    }

    private static Uri ResolveEpaperUri(Uri startPageUrl, NewsSource? source)
    {
        if (IsDirectPdfUrl(startPageUrl))
        {
            return new Uri(EpaperUrl);
        }

        if (startPageUrl.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
            && startPageUrl.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase))
        {
            return startPageUrl;
        }

        return source is not null ? ResolveEpaperUri(source, startPageUrl) : new Uri(EpaperUrl);
    }

    private static async Task<(Uri? PdfUrl, bool AccessBlocked)> ResolvePdfUrlAsync(
        IPage page,
        Uri epaperUri,
        int timeoutMs,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var blockedStreak = 0;
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Min(timeoutMs, 120_000));
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsDirectPdfUrl(new Uri(page.Url)))
            {
                return (StripTrackingQuery(new Uri(page.Url)), false);
            }

            var href = await TryReadAllPagesHrefAsync(page).ConfigureAwait(false);
            if (href is not null)
            {
                return (href, false);
            }

            var html = await page.ContentAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html))
            {
                if (ExtractPdfUrlFromHtml(html) is { } fromHtml)
                {
                    return (StripTrackingQuery(fromHtml), false);
                }

                if (PublisherAccessGuard.IsAccessBlocked(html))
                {
                    blockedStreak++;
                    logger.LogWarning(
                        "Al Ayam e-paper still behind access challenge (attempt {Attempt}); retrying…",
                        blockedStreak);
                    if (blockedStreak >= 8)
                    {
                        return (null, true);
                    }
                }
                else if (PublisherAccessGuard.LooksLikePublisherEpaper(html))
                {
                    blockedStreak = 0;
                }
            }

            try
            {
                await page.WaitForSelectorAsync(
                    AllPagesLinkSelector,
                    new PageWaitForSelectorOptions { Timeout = 3_000, State = WaitForSelectorState.Attached })
                    .ConfigureAwait(false);
                href = await TryReadAllPagesHrefAsync(page).ConfigureAwait(false);
                if (href is not null)
                {
                    return (href, false);
                }

                if (await TryClickAllPagesLinkAsync(page, cancellationToken).ConfigureAwait(false))
                {
                    await page.WaitForURLAsync(
                        url => url.Contains("i.alayam.com", StringComparison.OrdinalIgnoreCase)
                               && url.Contains("INAF_", StringComparison.OrdinalIgnoreCase)
                               && url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase),
                        new PageWaitForURLOptions { Timeout = Math.Min(timeoutMs, 60_000) })
                        .ConfigureAwait(false);
                    return (StripTrackingQuery(new Uri(page.Url)), false);
                }
            }
            catch (TimeoutException)
            {
                // keep polling
            }

            await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
        }

        return (null, blockedStreak >= 3);
    }

    private static async Task<Uri?> TryReadAllPagesHrefAsync(IPage page)
    {
        var locator = page.Locator(AllPagesLinkSelector);
        var count = await locator.CountAsync().ConfigureAwait(false);
        for (var i = 0; i < Math.Min(count, 5); i++)
        {
            var el = locator.Nth(i);
            var href = await el.GetAttributeAsync("href").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var resolved = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : new Uri(new Uri(page.Url), href).ToString();
            if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && IsDirectPdfUrl(uri))
            {
                return StripTrackingQuery(uri);
            }
        }

        return null;
    }

    private static async Task<bool> TryClickAllPagesLinkAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var locator = page.Locator("a#aPDFdownloadAllPages, a:has-text('كل الصفحات')");
        if (await locator.CountAsync().ConfigureAwait(false) == 0)
        {
            return false;
        }

        var link = locator.First;
        try
        {
            await link.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await link.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    private static Uri StripTrackingQuery(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Query))
        {
            return uri;
        }

        var builder = new UriBuilder(uri) { Query = string.Empty };
        return builder.Uri;
    }

    private static async Task<byte[]?> FetchPdfBytesViaPlaywrightRequestAsync(
        IBrowserContext? context,
        Uri pdfUrl,
        Uri referer,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return await FetchPdfBytesViaHttpAsync(pdfUrl, referer, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var response = await context.APIRequest.GetAsync(
                pdfUrl.ToString(),
                new()
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["Referer"] = referer.ToString(),
                        ["Accept"] = "application/pdf,*/*"
                    },
                    Timeout = timeoutMs
                }).ConfigureAwait(false);
            if (!response.Ok)
            {
                return null;
            }

            var bytes = await response.BodyAsync().ConfigureAwait(false);
            if (bytes is not null && PdfEditionContentFetcher.IsPdf(bytes))
            {
                return bytes;
            }
        }
        catch (Exception)
        {
            // fall through
        }

        return await FetchPdfBytesViaHttpAsync(pdfUrl, referer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]?> FetchPdfBytesViaHttpAsync(
        Uri pdfUrl,
        Uri referer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var request = new HttpRequestMessage(HttpMethod.Get, pdfUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", ResolveUserAgent());
            request.Headers.TryAddWithoutValidation("Referer", referer.ToString());
            request.Headers.TryAddWithoutValidation("Accept", "application/pdf,*/*");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public sealed record AlAyamFullEditionResult(Uri? PdfUrl, byte[]? PdfBytes, bool AccessBlocked = false);
