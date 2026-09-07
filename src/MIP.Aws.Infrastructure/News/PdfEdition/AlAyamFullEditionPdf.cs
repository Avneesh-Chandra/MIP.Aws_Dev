using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.Browser;
using MIP.Aws.Infrastructure.News;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Bahrain Al Ayam: Playwright clicks كل الصفحات on the e-paper page and saves the INAF PDF
/// from that browser session (same path as a manual download). HTTP is only a fallback.
/// </summary>
public static class AlAyamFullEditionPdf
{
    private const int HumanLikeRetryCount = 5;

    public const string EpaperUrl = "https://www.alayam.com/epaper";

    public const string AllPagesLinkSelector =
        "a#aPDFdownloadAllPages, a#aPDFdownloadAllPages span.allpages, a#aPDFdownloadAllPages span, a[href*='i.alayam.com'][href*='INAF_'][href$='.pdf']";

    private static readonly Regex AllPagesPdfHrefRegex = new(
        @"id=""aPDFdownloadAllPages""[^>]*href=""(?<url>https://i\.alayam\.com/[^""]+\.pdf)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InafPdfUrlRegex = new(
        @"https://i\.alayam\.com/ayamnewsa/upload/issue/\d+/\d+/PDF/INAF_[^""'\s<>]+\.pdf",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool UsesClickPath(NewsSource source) =>
        string.Equals(source.ConnectorKey, AlAyamEditionDiscovery.Key, StringComparison.OrdinalIgnoreCase);

    public static bool IsDirectPdfUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && IsDirectPdfUrl(uri);

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
        CancellationToken cancellationToken) =>
        await TryDiscoverAsync(
            startPageUrl,
            source,
            httpClientFactory,
            AlAyamOutboundHttpClient.NoOp,
            logger,
            cancellationToken).ConfigureAwait(false);

    public static async Task<AlAyamFullEditionResult?> TryDiscoverAsync(
        Uri startPageUrl,
        NewsSource source,
        IHttpClientFactory? httpClientFactory,
        IAlAyamOutboundHttpClient outbound,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // RPA parity: browser click path first; HTTP only as fallback.
        var fromClick = await RunClickPathAsync(
                startPageUrl,
                source,
                logger,
                downloadBytes: false,
                cancellationToken,
                processEvent: null)
            .ConfigureAwait(false);
        if (fromClick?.PdfUrl is not null || fromClick?.AccessBlocked == true)
        {
            return fromClick;
        }

        if (httpClientFactory is not null)
        {
            var knownPdf = IsDirectPdfUrl(startPageUrl) ? startPageUrl : null;
            var httpUrl = knownPdf
                          ?? await AlAyamHttpPdfPath.TryDiscoverPdfUrlAsync(
                              httpClientFactory,
                              outbound,
                              source,
                              logger,
                              cancellationToken)
                              .ConfigureAwait(false);
            if (httpUrl is not null)
            {
                return new AlAyamFullEditionResult(httpUrl, null);
            }
        }

        return fromClick;
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
        CancellationToken cancellationToken) =>
        await TryDownloadBytesAsync(
            startPageUrl,
            source,
            httpClientFactory,
            AlAyamOutboundHttpClient.NoOp,
            logger,
            cancellationToken).ConfigureAwait(false);

    public static async Task<byte[]?> TryDownloadBytesAsync(
        Uri startPageUrl,
        NewsSource? source,
        IHttpClientFactory? httpClientFactory,
        IAlAyamOutboundHttpClient outbound,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent = null)
    {
        var result = await RunClickPathAsync(
                startPageUrl,
                source,
                logger,
                downloadBytes: true,
                cancellationToken,
                processEvent)
            .ConfigureAwait(false);
        if (result?.PdfBytes is not null && PdfEditionContentFetcher.IsPdf(result.PdfBytes))
        {
            return result.PdfBytes;
        }

        if (httpClientFactory is not null && source is not null)
        {
            var knownPdf = IsDirectPdfUrl(startPageUrl) ? startPageUrl : result?.PdfUrl;
            var fromHttp = await AlAyamHttpPdfPath.TryDownloadBytesAsync(
                    httpClientFactory,
                    outbound,
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
        CancellationToken cancellationToken) =>
        await TryDownloadBytesWithFallbacksAsync(
            candidateOrPage,
            source,
            warmUpUrl,
            httpClientFactory,
            AlAyamOutboundHttpClient.NoOp,
            logger,
            cancellationToken).ConfigureAwait(false);

    public static async Task<byte[]?> TryDownloadBytesWithFallbacksAsync(
        Uri candidateOrPage,
        NewsSource source,
        Uri? warmUpUrl,
        IHttpClientFactory? httpClientFactory,
        IAlAyamOutboundHttpClient outbound,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent = null)
    {
        var starts = new List<Uri>();
        if (!IsDirectPdfUrl(candidateOrPage)
            && candidateOrPage.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
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
            EmitProcessEvent(processEvent, $"Starting Al Ayam acquisition from {start}.");
            var bytes = await TryDownloadBytesAsync(
                    start,
                    source,
                    httpClientFactory,
                    outbound,
                    logger,
                    cancellationToken,
                    processEvent)
                .ConfigureAwait(false);
            if (bytes is not null && bytes.Length > 0 && PdfEditionContentFetcher.IsPdf(bytes))
            {
                EmitProcessEvent(processEvent, $"Valid PDF bytes captured from {start} ({bytes.Length} bytes).");
                return bytes;
            }

            EmitProcessEvent(processEvent, $"No valid PDF returned from {start}; trying the next configured start URL.");
        }

        return null;
    }

    private static async Task<AlAyamFullEditionResult?> RunClickPathAsync(
        Uri startPageUrl,
        NewsSource? source,
        ILogger logger,
        bool downloadBytes,
        CancellationToken cancellationToken,
        Action<string>? processEvent)
    {
        var epaperUri = ResolveEpaperUri(startPageUrl, source);
        var timeoutMs = ResolveTimeoutMs(source);
        var userAgent = ResolveUserAgent();

        try
        {
            using var playwright = await PlaywrightBrowserLaunch.CreatePlaywrightAsync().ConfigureAwait(false);
            var launched = await PlaywrightBrowserLaunch
                .LaunchChromiumPreferHeadedAsync(playwright)
                .ConfigureAwait(false);
            await using var browser = launched.Browser;
            EmitProcessEvent(
                processEvent,
                launched.Headed
                    ? "Al Ayam Playwright launched headed (RPA-parity / Xvfb or DISPLAY)."
                    : "Al Ayam Playwright launched headless (no DISPLAY/Xvfb available).");
            logger.LogInformation(
                "Al Ayam click path browser mode: {Mode}.",
                launched.Headed ? "headed" : "headless");

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
                new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = timeoutMs })
                .ConfigureAwait(false);
            EmitProcessEvent(processEvent, $"Loaded Al Ayam e-paper page {epaperUri}.");

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Cloudflare or ads may prevent network idle.
            }

            var selector = source?.PdfLinkSelector?.Split(',')[0].Trim() ?? AllPagesLinkSelector;
            var accessBlocked = false;
            for (var attempt = 0; attempt <= HumanLikeRetryCount; attempt++)
            {
                // RPA-parity: mouse move + refresh before every attempt, including the first.
                if (!await PerformHumanLikeMouseMoveAndRefreshAsync(
                        page,
                        timeoutMs,
                        attempt,
                        afterBlock: attempt > 0,
                        logger,
                        cancellationToken,
                        processEvent)
                    .ConfigureAwait(false))
                {
                    break;
                }

                if (!await WaitForEpaperReadyAsync(
                        page,
                        selector,
                        source,
                        logger,
                        cancellationToken,
                        processEvent)
                    .ConfigureAwait(false))
                {
                    logger.LogWarning(
                        "Al Ayam e-paper page did not become ready on {Url} (attempt {Attempt}).",
                        epaperUri,
                        attempt + 1);
                    continue;
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

                if (downloadBytes)
                {
                    var captured = await ClickAllPagesAndCapturePdfAsync(
                            page,
                            timeoutMs,
                            logger,
                            cancellationToken,
                            processEvent)
                        .ConfigureAwait(false);
                    if (captured.Bytes is not null && PdfEditionContentFetcher.IsPdf(captured.Bytes))
                    {
                        logger.LogInformation(
                            "Al Ayam PDF saved after clicking كل الصفحات ({Bytes} bytes) from {Url}",
                            captured.Bytes.Length,
                            captured.PdfUrl);
                        EmitProcessEvent(
                            processEvent,
                            $"PDF captured after clicking كل الصفحات ({captured.Bytes.Length} bytes) from {captured.PdfUrl}.");
                        return new AlAyamFullEditionResult(captured.PdfUrl, captured.Bytes);
                    }
                }

                var resolve = await ResolvePdfUrlAsync(
                        page,
                        epaperUri,
                        timeoutMs,
                        logger,
                        cancellationToken,
                        processEvent)
                    .ConfigureAwait(false);
                accessBlocked |= resolve.AccessBlocked;
                if (resolve.PdfUrl is null)
                {
                    if (resolve.AccessBlocked)
                    {
                        logger.LogWarning(
                            "Al Ayam e-paper blocked by publisher bot protection on {Url} (attempt {Attempt}).",
                            epaperUri,
                            attempt + 1);
                    }

                    continue;
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
                if (bytes is not null && bytes.Length > 0 && PdfEditionContentFetcher.IsPdf(bytes))
                {
                    logger.LogInformation("Al Ayam PDF downloaded ({Bytes} bytes) from {Url}", bytes.Length, resolve.PdfUrl);
                    EmitProcessEvent(
                        processEvent,
                        $"PDF downloaded from resolved direct URL ({bytes.Length} bytes): {resolve.PdfUrl}");
                    return new AlAyamFullEditionResult(resolve.PdfUrl, bytes);
                }

                logger.LogWarning("Al Ayam click path: PDF fetch failed for {Url} (attempt {Attempt}).", resolve.PdfUrl, attempt + 1);
            }

            return accessBlocked
                ? new AlAyamFullEditionResult(null, null, AccessBlocked: true)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Al Ayam all-pages click path failed from {Url}", epaperUri);
            return null;
        }
    }

    private static async Task<bool> PerformHumanLikeMouseMoveAndRefreshAsync(
        IPage page,
        int timeoutMs,
        int attempt,
        bool afterBlock,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var attemptNumber = attempt + 1;
            if (afterBlock)
            {
                logger.LogInformation(
                    "Al Ayam block detected; performing human-like retry {RetryAttempt} with immediate mouse movement and refresh.",
                    attempt);
                EmitProcessEvent(
                    processEvent,
                    $"Block detected; moved mouse and immediately refreshed page (retry {attempt} of {HumanLikeRetryCount}).");
            }
            else
            {
                logger.LogInformation(
                    "Al Ayam performing human-like mouse movement and refresh before attempt {Attempt}.",
                    attemptNumber);
                EmitProcessEvent(
                    processEvent,
                    $"Moved mouse and refreshed page before attempt {attemptNumber}.");
            }

            await page.Mouse.MoveAsync(220, 180).ConfigureAwait(false);
            await page.ReloadAsync(
                new PageReloadOptions { WaitUntil = WaitUntilState.Load, Timeout = timeoutMs })
                .ConfigureAwait(false);

            try
            {
                await page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions { Timeout = 30_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Ads or the publisher challenge may prevent network idle.
            }

            return true;
        }
        catch (PlaywrightException ex)
        {
            logger.LogWarning(ex, "Al Ayam human-like mouse move and refresh failed.");
            return false;
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

    private static async Task<bool> WaitForEpaperReadyAsync(
        IPage page,
        string selector,
        NewsSource? source,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent)
    {
        var maxAttempts = source is null
            ? 30
            : Math.Max(30, Math.Clamp(source.DownloadWaitTimeoutSeconds, 60, 600) / 2);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await page.Locator(selector).CountAsync().ConfigureAwait(false) > 0)
            {
                return true;
            }

            var html = await page.ContentAsync().ConfigureAwait(false);
            if (PublisherAccessGuard.LooksLikePublisherEpaper(html))
            {
                return true;
            }

            if (PublisherAccessGuard.IsAccessBlocked(html))
            {
                logger.LogDebug("Al Ayam: waiting for Cloudflare challenge (attempt {Attempt})", attempt + 1);
                EmitProcessEvent(processEvent, "Blocked page detected before edition link became ready.");
                return false;
            }
            else if (ExtractPdfUrlFromHtml(html) is not null)
            {
                return true;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        var finalHtml = await page.ContentAsync().ConfigureAwait(false);
        return await page.Locator(selector).CountAsync().ConfigureAwait(false) > 0
               || PublisherAccessGuard.LooksLikePublisherEpaper(finalHtml);
    }

    private static async Task<(Uri? PdfUrl, bool AccessBlocked)> ResolvePdfUrlAsync(
        IPage page,
        Uri epaperUri,
        int timeoutMs,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent)
    {
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
                EmitProcessEvent(processEvent, $"Resolved current direct PDF URL: {href}");
                return (href, false);
            }

            var html = await page.ContentAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html))
            {
                if (ExtractPdfUrlFromHtml(html) is { } fromHtml)
                {
                    EmitProcessEvent(processEvent, $"Resolved current direct PDF URL from page HTML: {fromHtml}");
                    return (StripTrackingQuery(fromHtml), false);
                }

                if (PublisherAccessGuard.IsAccessBlocked(html))
                {
                    logger.LogWarning(
                        "Al Ayam e-paper is blocked; returning immediately for human-like refresh retry.");
                    EmitProcessEvent(processEvent, "Blocked page detected while resolving the direct PDF URL.");
                    return (null, true);
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
                    EmitProcessEvent(processEvent, $"Resolved current direct PDF URL after waiting: {href}");
                    return (href, false);
                }

                if (await TryClickAllPagesLinkAsync(page, cancellationToken).ConfigureAwait(false))
                {
                    await page.WaitForURLAsync(
                        IsDirectPdfUrl,
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

        return (null, false);
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

    private static async Task<(Uri? PdfUrl, byte[]? Bytes)> ClickAllPagesAndCapturePdfAsync(
        IPage page,
        int timeoutMs,
        ILogger logger,
        CancellationToken cancellationToken,
        Action<string>? processEvent)
    {
        var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = timeoutMs });
        var responseTask = page.WaitForResponseAsync(
            IsAlAyamPdfResponse,
            new PageWaitForResponseOptions { Timeout = timeoutMs });
        var extraPageTask = page.Context.WaitForPageAsync(new BrowserContextWaitForPageOptions { Timeout = timeoutMs });
        ObserveFault(downloadTask);
        ObserveFault(responseTask);
        ObserveFault(extraPageTask);

        if (!await TryClickAllPagesLinkAsync(page, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Al Ayam: كل الصفحات was not clickable on the e-paper page.");
            EmitProcessEvent(processEvent, "The كل الصفحات control was not clickable.");
            return (null, null);
        }

        logger.LogInformation("Al Ayam clicked كل الصفحات; capturing the PDF from the browser session.");
        EmitProcessEvent(processEvent, "Clicked كل الصفحات; waiting for browser download or PDF response.");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var triedViewerFetch = false;
        var triedExtraPage = false;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentHtml = await page.ContentAsync().ConfigureAwait(false);
            if (PublisherAccessGuard.IsAccessBlocked(currentHtml))
            {
                logger.LogWarning(
                    "Al Ayam block page appeared after clicking كل الصفحات; returning immediately for refresh retry.");
                EmitProcessEvent(processEvent, "Blocked page appeared after clicking كل الصفحات.");
                return (null, null);
            }

            var fromDownload = await TryReadCompletedDownloadAsync(downloadTask, cancellationToken).ConfigureAwait(false);
            if (fromDownload.Bytes is not null && PdfEditionContentFetcher.IsPdf(fromDownload.Bytes))
            {
                return fromDownload;
            }

            if (responseTask.IsCompletedSuccessfully)
            {
                var response = await responseTask.ConfigureAwait(false);
                var fromResponse = await TryReadResponsePdfAsync(response).ConfigureAwait(false);
                if (fromResponse.Bytes is not null && PdfEditionContentFetcher.IsPdf(fromResponse.Bytes))
                {
                    return fromResponse;
                }
            }

            if (!triedExtraPage && extraPageTask.IsCompletedSuccessfully)
            {
                triedExtraPage = true;
                var extraPage = await extraPageTask.ConfigureAwait(false);
                extraPage.SetDefaultTimeout(timeoutMs);
                var fromPopup = await TryCapturePdfFromOpenedPageAsync(extraPage, timeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                if (fromPopup.Bytes is not null && PdfEditionContentFetcher.IsPdf(fromPopup.Bytes))
                {
                    return fromPopup;
                }
            }

            if (!triedViewerFetch && IsDirectPdfUrl(page.Url))
            {
                triedViewerFetch = true;
                var fromViewer = await TryCapturePdfFromOpenedPageAsync(page, timeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                if (fromViewer.Bytes is not null && PdfEditionContentFetcher.IsPdf(fromViewer.Bytes))
                {
                    return fromViewer;
                }
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return (IsDirectPdfUrl(page.Url) ? StripTrackingQuery(new Uri(page.Url)) : null, null);
    }

    private static bool IsAlAyamPdfResponse(IResponse response) =>
        response.Status is >= 200 and < 400 && IsDirectPdfUrl(response.Url);

    private static async Task<(Uri? PdfUrl, byte[]? Bytes)> TryReadCompletedDownloadAsync(
        Task<IDownload> downloadTask,
        CancellationToken cancellationToken)
    {
        if (!downloadTask.IsCompletedSuccessfully)
        {
            return (null, null);
        }

        try
        {
            var download = await downloadTask.ConfigureAwait(false);
            var path = await download.PathAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return (null, null);
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Uri.TryCreate(download.Url, UriKind.Absolute, out var url);
            return (url is not null && IsDirectPdfUrl(url) ? StripTrackingQuery(url) : url, bytes);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static async Task<(Uri? PdfUrl, byte[]? Bytes)> TryReadResponsePdfAsync(IResponse response)
    {
        try
        {
            var bytes = await response.BodyAsync().ConfigureAwait(false);
            Uri.TryCreate(response.Url, UriKind.Absolute, out var url);
            return (url is not null ? StripTrackingQuery(url) : null, bytes);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static async Task<(Uri? PdfUrl, byte[]? Bytes)> TryCapturePdfFromOpenedPageAsync(
        IPage page,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsDirectPdfUrl(page.Url))
        {
            try
            {
                await page.WaitForURLAsync(
                    IsDirectPdfUrl,
                    new PageWaitForURLOptions { Timeout = Math.Min(timeoutMs, 30_000) })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return (null, null);
            }
        }

        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out var pdfUrl) || !IsDirectPdfUrl(pdfUrl))
        {
            return (null, null);
        }

        var fromContext = await FetchPdfBytesViaPlaywrightRequestAsync(
                page.Context,
                pdfUrl,
                new Uri(EpaperUrl),
                timeoutMs,
                cancellationToken)
            .ConfigureAwait(false);
        return (StripTrackingQuery(pdfUrl), fromContext);
    }

    private static async Task<bool> TryClickAllPagesLinkAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Match UiPath Alayam.xaml: click SPAN كل الصفحات under #aPDFdownloadAllPages (60s timeout).
        const int clickTimeoutMs = 60_000;
        ILocator[] locators =
        [
            page.Locator("a#aPDFdownloadAllPages span.allpages"),
            page.Locator("a#aPDFdownloadAllPages span"),
            page.Locator("a#aPDFdownloadAllPages"),
            page.Locator("#aPDFdownloadAllPages"),
            page.GetByRole(AriaRole.Link, new() { Name = "كل الصفحات" }),
            page.Locator("a").Filter(new LocatorFilterOptions { HasText = "كل الصفحات" }),
            page.GetByText("كل الصفحات", new PageGetByTextOptions { Exact = true })
        ];

        foreach (var locator in locators)
        {
            try
            {
                if (await locator.CountAsync().ConfigureAwait(false) == 0)
                {
                    continue;
                }

                var target = locator.First;
                await target.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                await target.ClickAsync(new LocatorClickOptions { Timeout = clickTimeoutMs }).ConfigureAwait(false);
                return true;
            }
            catch (PlaywrightException)
            {
                // Try the next locator that matches the on-page كل الصفحات control.
            }
        }

        return false;
    }

    private static void ObserveFault(Task task) =>
        task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static void EmitProcessEvent(Action<string>? processEvent, string message) =>
        processEvent?.Invoke(message);

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
