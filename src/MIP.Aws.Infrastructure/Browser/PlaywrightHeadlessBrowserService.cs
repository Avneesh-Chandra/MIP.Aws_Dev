using MIP.Aws.Application.Abstractions.Browser;
using MIP.Aws.Infrastructure.News;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Browser;

/// <summary>
/// Optional Playwright rendering. Falls back gracefully when browsers are not installed on the host.
/// </summary>
public sealed class PlaywrightHeadlessBrowserService(ILogger<PlaywrightHeadlessBrowserService> logger) : IHeadlessBrowserService
{
    private const string DesktopChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";

    public async Task<string?> GetRenderedHtmlAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = ["--disable-blink-features=AutomationControlled"]
            }).ConfigureAwait(false);

            var locale = ResolveLocale(url);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = DesktopChromeUserAgent,
                Locale = locale,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
                AcceptDownloads = true
            }).ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout((float)timeout.TotalMilliseconds);

            await page.GotoAsync(
                url.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = (float)timeout.TotalMilliseconds })
                .ConfigureAwait(false);

            await WaitForPublisherContentAsync(page, url, timeout, cancellationToken).ConfigureAwait(false);

            var html = await page.ContentAsync().ConfigureAwait(false);
            if (PublisherAccessGuard.IsAccessBlocked(html))
            {
                logger.LogWarning("Playwright capture for {Url} still returned a blocked/challenge page.", url);
            }

            return html;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Playwright capture failed for {Url}", url);
            return null;
        }
    }

    private static string ResolveLocale(Uri url)
    {
        if (url.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
        {
            return "ar-BH";
        }

        if (url.Host.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase))
        {
            return "ar-SA";
        }

        return "en-US";
    }

    private static async Task WaitForPublisherContentAsync(
        IPage page,
        Uri url,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var waitMs = Math.Min((int)timeout.TotalMilliseconds, 45_000);

        if (url.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await page.WaitForSelectorAsync(
                    "a#aPDFdownloadAllPages, a[href*='i.alayam.com'][href$='.pdf']",
                    new PageWaitForSelectorOptions { Timeout = waitMs, State = WaitForSelectorState.Attached })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Fall through with whatever HTML is available.
            }

            return;
        }

        if (url.Host.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase)
            && url.AbsolutePath.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await page.WaitForSelectorAsync(
                    "button[aria-label='Download'], a:has-text('Full Publication')",
                    new PageWaitForSelectorOptions { Timeout = waitMs, State = WaitForSelectorState.Attached })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }
    }
}
