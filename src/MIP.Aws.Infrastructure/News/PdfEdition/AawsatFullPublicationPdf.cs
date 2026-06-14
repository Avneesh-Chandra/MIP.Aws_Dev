using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Asharq Al-Awsat: homepage issue icon → issue viewer → Download → Full Publication → PDF file.
/// </summary>
public static class AawsatFullPublicationPdf
{
    public const string HomeIssueLinkSelector = "a.pdf-btn[href*='/files/pdf/issue'], a.ico-pdf[href*='/files/pdf/issue'], a[href*='/files/pdf/issue']";

    public const string DefaultDownloadSelector = "button[aria-label='Download']";

    public const string DefaultFullPublicationSelector = "a:has-text('Full Publication')";

    private static readonly string[] DownloadTextLabels = ["Download", "تحميل", "تنزيل"];
    private static readonly string[] FullPublicationTextLabels = ["Full Publication", "النسخة الكاملة", "النشرة الكاملة"];

    public static bool IsIssueViewerUrl(Uri url) =>
        url.AbsolutePath.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Issue folder URLs (e.g. /files/pdf/issue17362/) must open the viewer page (/2/index.html) for the Download control.
    /// </summary>
    public static Uri ResolveIssueViewerUri(Uri issueUri)
    {
        if (!IsIssueViewerUrl(issueUri))
        {
            return issueUri;
        }

        var path = issueUri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            return issueUri;
        }

        if (path.EndsWith("/2", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/1", StringComparison.OrdinalIgnoreCase))
        {
            return new UriBuilder(issueUri) { Path = path + "/index.html" }.Uri;
        }

        return new UriBuilder(issueUri) { Path = path + "/2/index.html" }.Uri;
    }

    public static Uri ResolvePlaywrightStartUri(Uri candidateOrPage, Uri? warmUpUrl)
    {
        if (IsIssueViewerUrl(candidateOrPage))
        {
            return ResolveIssueViewerUri(candidateOrPage);
        }

        if (warmUpUrl is not null && IsIssueViewerUrl(warmUpUrl))
        {
            return ResolveIssueViewerUri(warmUpUrl);
        }

        return warmUpUrl ?? candidateOrPage;
    }

    public static bool UsesClickPath(NewsSource source) =>
        string.Equals(source.ConnectorKey, AawsatEditionDiscovery.Key, StringComparison.OrdinalIgnoreCase);

    public static string ResolveDownloadSelector(NewsSource source) =>
        string.IsNullOrWhiteSpace(source.PdfDownloadSelector) ? DefaultDownloadSelector : source.PdfDownloadSelector.Trim();

    public static string ResolveFullPublicationSelector(NewsSource source) =>
        string.IsNullOrWhiteSpace(source.PdfLinkSelector) ? DefaultFullPublicationSelector : source.PdfLinkSelector.Trim();

    public static Task<AawsatFullPublicationResult?> TryDiscoverAsync(
        Uri startPageUrl,
        NewsSource source,
        ILogger logger,
        CancellationToken cancellationToken) =>
        RunClickPathAsync(
            startPageUrl,
            ResolveDownloadSelector(source),
            ResolveFullPublicationSelector(source),
            logger,
            cancellationToken);

    public static async Task<byte[]?> TryDownloadBytesAsync(
        Uri startPageUrl,
        NewsSource? source,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = await RunClickPathAsync(
            startPageUrl,
            source is null ? DefaultDownloadSelector : ResolveDownloadSelector(source),
            source is null ? DefaultFullPublicationSelector : ResolveFullPublicationSelector(source),
            logger,
            cancellationToken).ConfigureAwait(false);
        return result?.PdfBytes;
    }

    /// <summary>
    /// Tries the Full Publication click path from multiple entry pages (homepage, warm-up, issue viewer).
    /// </summary>
    public static async Task<byte[]?> TryDownloadBytesWithFallbacksAsync(
        Uri candidateOrPage,
        NewsSource source,
        Uri? warmUpUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var starts = new List<Uri>();
        if (!string.IsNullOrWhiteSpace(source.PdfDiscoveryPageUrl)
            && Uri.TryCreate(source.PdfDiscoveryPageUrl.Trim(), UriKind.Absolute, out var discoveryPage))
        {
            starts.Add(discoveryPage);
        }

        if (!string.IsNullOrWhiteSpace(source.BaseUrl)
            && Uri.TryCreate(source.BaseUrl.Trim(), UriKind.Absolute, out var basePage))
        {
            starts.Add(basePage);
        }

        if (warmUpUrl is not null)
        {
            starts.Add(ResolvePlaywrightStartUri(warmUpUrl, null));
        }

        starts.Add(ResolvePlaywrightStartUri(candidateOrPage, warmUpUrl));

        foreach (var start in starts
                     .Where(u => u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
                     .DistinctBy(u => u.ToString().TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
        {
            var bytes = await TryDownloadBytesAsync(start, source, logger, cancellationToken).ConfigureAwait(false);
            if (bytes is not null && bytes.Length > 0 && PdfEditionContentFetcher.IsPdf(bytes))
            {
                return bytes;
            }
        }

        return null;
    }

    private static async Task<AawsatFullPublicationResult?> RunClickPathAsync(
        Uri startPageUrl,
        string downloadSelector,
        string fullPublicationSelector,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await LaunchBrowserAsync(playwright).ConfigureAwait(false);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true,
                Locale = "ar-SA",
                UserAgent = DesktopChromeUserAgent,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            }).ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(90_000);
            await page.GotoAsync(
                startPageUrl.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 }).ConfigureAwait(false);
            await Task.Delay(1_500, cancellationToken).ConfigureAwait(false);

            var issuePage = await OpenIssuePageAsync(page, startPageUrl, cancellationToken).ConfigureAwait(false);
            if (issuePage is null)
            {
                logger.LogWarning("Aawsat click path: no issue page opened from {Url}", startPageUrl);
                return null;
            }

            await issuePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await Task.Delay(1_500, cancellationToken).ConfigureAwait(false);

            if (!await ClickFirstVisibleInPageOrFramesAsync(
                    issuePage,
                    downloadSelector,
                    DownloadTextLabels,
                    cancellationToken).ConfigureAwait(false))
            {
                logger.LogWarning("Aawsat click path: Download control not found on {Url}", issuePage.Url);
                return null;
            }

            await Task.Delay(750, cancellationToken).ConfigureAwait(false);

            var fullPublicationLink = issuePage.Locator(fullPublicationSelector).First;
            if (await fullPublicationLink.CountAsync().ConfigureAwait(false) == 0)
            {
                foreach (var label in FullPublicationTextLabels)
                {
                    var byText = issuePage.GetByText(label, new PageGetByTextOptions { Exact = false });
                    if (await byText.CountAsync().ConfigureAwait(false) > 0)
                    {
                        fullPublicationLink = byText.First;
                        break;
                    }
                }
            }

            await fullPublicationLink.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 }).ConfigureAwait(false);

            var downloadTask = issuePage.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = 90_000 });
            await fullPublicationLink.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
            var download = await downloadTask.ConfigureAwait(false);

            return await ReadDownloadAsync(download, issuePage, startPageUrl, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aawsat Full Publication click path failed from {Url}", startPageUrl);
            return null;
        }
    }

    private const string DesktopChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";

    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-blink-features=AutomationControlled"]
        });

    private static async Task<AawsatFullPublicationResult?> ReadDownloadAsync(
        IDownload download,
        IPage issuePage,
        Uri startPageUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var stream = await download.CreateReadStreamAsync().ConfigureAwait(false);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var bytes = ms.ToArray();

        if (bytes.Length == 0 || !PdfEditionContentFetcher.IsPdf(bytes))
        {
            logger.LogWarning(
                "Aawsat Full Publication download was not a PDF ({Bytes} bytes, url {Url})",
                bytes.Length,
                download.Url);
            return null;
        }

        var candidateUrl = ResolveCandidateUrl(download, issuePage, startPageUrl);
        logger.LogInformation(
            "Aawsat Full Publication PDF downloaded ({Bytes} bytes, url {Url}, suggested {Name})",
            bytes.Length,
            candidateUrl,
            download.SuggestedFilename);
        return new AawsatFullPublicationResult(candidateUrl, bytes);
    }

    private static Uri ResolveCandidateUrl(IDownload download, IPage issuePage, Uri startPageUrl)
    {
        if (Uri.TryCreate(download.Url, UriKind.Absolute, out var downloadUri)
            && (downloadUri.Scheme == Uri.UriSchemeHttp || downloadUri.Scheme == Uri.UriSchemeHttps))
        {
            return downloadUri;
        }

        if (Uri.TryCreate(issuePage.Url, UriKind.Absolute, out var issueUri)
            && issueUri.AbsolutePath.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase))
        {
            return issueUri;
        }

        return startPageUrl;
    }

    private static async Task<IPage?> OpenIssuePageAsync(
        IPage entryPage,
        Uri startPageUrl,
        CancellationToken cancellationToken)
    {
        if (startPageUrl.AbsolutePath.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase))
        {
            var viewerUri = ResolveIssueViewerUri(startPageUrl);
            var target = viewerUri.ToString();
            if (!string.Equals(entryPage.Url, target, StringComparison.OrdinalIgnoreCase))
            {
                await entryPage.GotoAsync(
                    target,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 }).ConfigureAwait(false);
            }

            return entryPage;
        }

        return await OpenLatestIssuePageFromHomeAsync(entryPage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IPage?> OpenLatestIssuePageFromHomeAsync(IPage homePage, CancellationToken cancellationToken)
    {
        var issueLink = homePage.Locator(HomeIssueLinkSelector).First;
        if (await issueLink.CountAsync().ConfigureAwait(false) == 0)
        {
            return null;
        }

        await issueLink.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 }).ConfigureAwait(false);
        var href = await issueLink.GetAttributeAsync("href").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var issueUri = Uri.TryCreate(href, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(new Uri(homePage.Url), href);

        var issuePage = await homePage.Context.NewPageAsync().ConfigureAwait(false);
        var viewerUri = ResolveIssueViewerUri(issueUri);
        await issuePage.GotoAsync(
            viewerUri.ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 }).ConfigureAwait(false);
        return issuePage;
    }

    private static async Task<bool> ClickFirstVisibleInPageOrFramesAsync(
        IPage page,
        string configuredSelector,
        IReadOnlyList<string> textLabels,
        CancellationToken cancellationToken)
    {
        if (await ClickFirstVisibleAsync(page, configuredSelector, textLabels, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        foreach (var frame in page.Frames)
        {
            if (frame == page.MainFrame)
            {
                continue;
            }

            try
            {
                if (await ClickFirstVisibleInFrameAsync(frame, configuredSelector, textLabels, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch (PlaywrightException)
            {
                // try next frame
            }
        }

        return false;
    }

    private static Task<bool> ClickFirstVisibleInFrameAsync(
        IFrame frame,
        string configuredSelector,
        IReadOnlyList<string> textLabels,
        CancellationToken cancellationToken) =>
        ClickLocatorsAsync(
            selector => frame.Locator(selector).First,
            label => frame.GetByText(label, new FrameGetByTextOptions { Exact = false }),
            configuredSelector,
            textLabels,
            cancellationToken);

    private static Task<bool> ClickFirstVisibleAsync(
        IPage page,
        string configuredSelector,
        IReadOnlyList<string> textLabels,
        CancellationToken cancellationToken) =>
        ClickLocatorsAsync(
            selector => page.Locator(selector).First,
            label => page.GetByText(label, new PageGetByTextOptions { Exact = false }),
            configuredSelector,
            textLabels,
            cancellationToken);

    private static async Task<bool> ClickLocatorsAsync(
        Func<string, ILocator> locatorFactory,
        Func<string, ILocator> textLocatorFactory,
        string configuredSelector,
        IReadOnlyList<string> textLabels,
        CancellationToken cancellationToken)
    {
        foreach (var selector in configuredSelector.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var locator = locatorFactory(selector);
            if (await locator.CountAsync().ConfigureAwait(false) == 0)
            {
                continue;
            }

            try
            {
                await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8_000 }).ConfigureAwait(false);
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
                return true;
            }
            catch (PlaywrightException)
            {
                // try next selector
            }
        }

        foreach (var label in textLabels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var byText = textLocatorFactory(label);
            if (await byText.CountAsync().ConfigureAwait(false) == 0)
            {
                continue;
            }

            try
            {
                await byText.First.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
                return true;
            }
            catch (PlaywrightException)
            {
                // try next label
            }
        }

        return false;
    }
}

public sealed record AawsatFullPublicationResult(Uri PdfUrl, byte[] PdfBytes);
