using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PlaywrightPdfEditionDiscoveryService(
    EditionDiscoveryHtmlClient htmlClient,
    EditionUrlDiscoveryRegistry editionRegistry,
    ILogger<PlaywrightPdfEditionDiscoveryService> logger) : IPdfEditionDiscoveryService
{
    public async Task<PdfEditionDiscoveryResult> DiscoverAsync(NewsSource source, bool allowPlaywright, CancellationToken cancellationToken)
    {
        if (!source.PdfDiscoveryEnabled)
        {
            throw new InvalidOperationException("PDF discovery is not enabled for this source.");
        }

        EnsureSupportedSourceType(source);

        var pageUrl = ResolvePageUrl(source);
        var candidates = new List<PdfEditionCandidate>();

        var useHeadless = allowPlaywright && source.UseHeadlessBrowser;
        var aawsatClickPath = allowPlaywright && AawsatFullPublicationPdf.UsesClickPath(source);

        if (aawsatClickPath)
        {
            try
            {
                var discovered = await AawsatFullPublicationPdf.TryDiscoverAsync(
                        pageUrl,
                        source,
                        logger,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (discovered is not null)
                {
                    candidates.Add(new PdfEditionCandidate(
                        discovered.PdfUrl,
                        0.99,
                        PdfDiscoveryMethod.ConfiguredDownloadSelector,
                        "Full Publication",
                        true));
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Aawsat click-path discovery did not yield a PDF for {Source}", source.Name);
            }
        }

        // 1) Aawsat or other connectors: resolve edition URL via HTTP where possible.
        await TryAddEditionConnectorCandidatesAsync(source, candidates, cancellationToken).ConfigureAwait(false);

        // 2) Selector-based link extraction (requires Playwright; skipped for Aawsat click path)
        if (!aawsatClickPath &&
            allowPlaywright &&
            !string.IsNullOrWhiteSpace(source.PdfLinkSelector) &&
            source.PdfDiscoveryMode is PdfDiscoveryMode.ManualSelector or PdfDiscoveryMode.Hybrid)
        {
            var fromSelector = await TryLinkSelectorAsync(pageUrl, source, cancellationToken).ConfigureAwait(false);
            candidates.AddRange(fromSelector);
        }

        // 4) HTML auto-scan / keyword
        if (!aawsatClickPath &&
            source.PdfDiscoveryMode is PdfDiscoveryMode.AutoDetectPdfLink or PdfDiscoveryMode.KeywordBased or PdfDiscoveryMode.Hybrid)
        {
            var html = await htmlClient.FetchHtmlAsync(pageUrl, useHeadless, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html))
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                foreach (var scored in PdfEditionCandidateScorer.ScoreFromHtml(html, pageUrl, source, today))
                {
                    candidates.Add(new PdfEditionCandidate(
                        scored.Url,
                        scored.Confidence,
                        PdfDiscoveryMethod.AutoScan,
                        scored.Label,
                        scored.IsTodayEdition));
                }
            }
        }

        // 5) Generic Playwright click on download selector (non-Aawsat)
        if (!aawsatClickPath &&
            allowPlaywright &&
            !string.IsNullOrWhiteSpace(source.PdfDownloadSelector) &&
            source.PdfDiscoveryMode is PdfDiscoveryMode.ManualSelector or PdfDiscoveryMode.Hybrid)
        {
            var fromClick = await TryDownloadSelectorAsync(pageUrl, source, cancellationToken).ConfigureAwait(false);
            if (fromClick is not null)
            {
                candidates.Add(fromClick);
            }
        }

        var ordered = candidates
            .GroupBy(c => c.Url.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Confidence).First())
            .OrderByDescending(c => c.IsTodayEdition && source.PreferTodayEdition)
            .ThenByDescending(c => c.Confidence)
            .ToList();

        var best = ordered.FirstOrDefault();
        logger.LogInformation(
            "PDF discovery for {Source}: {Count} candidate(s), best={Best}",
            source.Name,
            ordered.Count,
            best?.Url);

        string? discoveryFailureReason = null;
        if (ordered.Count == 0)
        {
            var probeHtml = await htmlClient.FetchHtmlAsync(pageUrl, useHeadless, cancellationToken).ConfigureAwait(false);
            if (PublisherAccessGuard.IsAccessBlocked(probeHtml))
            {
                discoveryFailureReason =
                    "Publisher blocked automated access (Cloudflare/bot protection) on the e-paper page.";
            }
        }

        return new PdfEditionDiscoveryResult(ordered, best, pageUrl.ToString(), discoveryFailureReason);
    }

    private static void EnsureSupportedSourceType(NewsSource source)
    {
        if (source.SourceType is not (NewsSourceType.PublicHtml or NewsSourceType.PublicPdf
            or NewsSourceType.WebPortalLogin))
        {
            throw new InvalidOperationException($"PDF discovery is not supported for source type {source.SourceType}.");
        }

        if (source.SourceType == NewsSourceType.WebPortalLogin && source.RequiresLogin && !source.IsDownloadAllowed)
        {
            throw new InvalidOperationException("Portal source requires legal authentication before PDF discovery.");
        }
    }

    private async Task TryAddEditionConnectorCandidatesAsync(
        NewsSource source,
        List<PdfEditionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (!editionRegistry.Contains(source.ConnectorKey))
        {
            return;
        }

        try
        {
            var edition = await editionRegistry.GetRequired(source.ConnectorKey)
                .DiscoverLatestEditionAsync(source, cancellationToken)
                .ConfigureAwait(false);

            if (!ShouldAddEditionConnectorCandidate(edition, source))
            {
                return;
            }

            var method = AawsatFullPublicationPdf.UsesClickPath(source)
                && AawsatFullPublicationPdf.IsIssueViewerUrl(edition.ResourceUri)
                ? PdfDiscoveryMethod.ConfiguredDownloadSelector
                : PdfDiscoveryMethod.DirectPdfHref;
            var label = method == PdfDiscoveryMethod.ConfiguredDownloadSelector
                ? "Full Publication"
                : edition.Title ?? "Latest edition PDF";

            candidates.Add(new PdfEditionCandidate(
                edition.ResourceUri,
                method == PdfDiscoveryMethod.ConfiguredDownloadSelector ? 0.98 : 0.93,
                method,
                label,
                true));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Edition connector discovery did not yield a PDF for {Source}", source.Name);
        }
    }

    private static bool ShouldAddEditionConnectorCandidate(EditionDiscoveryResult edition, NewsSource source) =>
        LooksLikeDirectPdfEdition(edition)
        || (AawsatFullPublicationPdf.UsesClickPath(source)
            && AawsatFullPublicationPdf.IsIssueViewerUrl(edition.ResourceUri));

    private static bool LooksLikeDirectPdfEdition(EditionDiscoveryResult edition) =>
        edition.ContentTypeHint.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || edition.ResourceUri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static Uri ResolvePageUrl(NewsSource source)
    {
        var raw = string.IsNullOrWhiteSpace(source.PdfDiscoveryPageUrl)
            ? source.BaseUrl
            : source.PdfDiscoveryPageUrl;
        return new Uri(raw.TrimEnd('/') + (raw.EndsWith('/') ? "" : "/"));
    }

    private async Task<IReadOnlyList<PdfEditionCandidate>> TryLinkSelectorAsync(
        Uri pageUrl,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            var page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(pageUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 }).ConfigureAwait(false);

            var locator = page.Locator(source.PdfLinkSelector!);
            var count = await locator.CountAsync().ConfigureAwait(false);
            var results = new List<PdfEditionCandidate>();
            for (var i = 0; i < Math.Min(count, 5); i++)
            {
                var el = locator.Nth(i);
                if (!await el.IsVisibleAsync().ConfigureAwait(false))
                {
                    continue;
                }

                var href = await el.GetAttributeAsync("href").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                if (!Uri.TryCreate(pageUrl, href, out var uri))
                {
                    continue;
                }

                var text = (await el.InnerTextAsync().ConfigureAwait(false))?.Trim();
                var confidence = PdfEditionCandidateScorer.ScoreHref(uri.ToString(), text, null, source, DateOnly.FromDateTime(DateTime.UtcNow));
                results.Add(new PdfEditionCandidate(uri, Math.Max(confidence, 0.75), PdfDiscoveryMethod.ConfiguredLinkSelector, text, false));
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PdfLinkSelector discovery failed for {Source}", source.Name);
            return Array.Empty<PdfEditionCandidate>();
        }
    }

    private async Task<PdfEditionCandidate?> TryDownloadSelectorAsync(
        Uri pageUrl,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            var context = await browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true }).ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(pageUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 }).ConfigureAwait(false);

            var locator = page.Locator(source.PdfDownloadSelector!);
            if (!await locator.First.IsVisibleAsync().ConfigureAwait(false))
            {
                return null;
            }

            // Popup navigation
            var popupTask = page.WaitForPopupAsync(new PageWaitForPopupOptions { Timeout = 15_000 });
            await locator.First.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
            try
            {
                var popup = await popupTask.ConfigureAwait(false);
                var popupUrl = popup.Url;
                if (popupUrl.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return new PdfEditionCandidate(new Uri(popupUrl), 0.85, PdfDiscoveryMethod.PlaywrightPopup, "popup PDF", true);
                }
            }
            catch (TimeoutException)
            {
                // no popup — check current URL
            }

            var current = page.Url;
            if (current.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new PdfEditionCandidate(new Uri(current), 0.8, PdfDiscoveryMethod.PlaywrightClick, "navigation PDF", true);
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PdfDownloadSelector click failed for {Source}", source.Name);
            return null;
        }
    }
}
