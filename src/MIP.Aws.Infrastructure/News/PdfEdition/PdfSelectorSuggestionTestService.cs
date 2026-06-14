using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfSelectorSuggestionTestService(
    PdfEditionValidator validator,
    ILogger<PdfSelectorSuggestionTestService> logger) : IPdfSelectorSuggestionTestService
{
    public async Task<PdfSelectorSuggestionTestOutcome> TestAsync(
        string pageUrl,
        string selector,
        string expectedAction,
        bool useHeadlessBrowser,
        bool requirePdfContent,
        int minimumSizeKb,
        CancellationToken cancellationToken)
    {
        if (!SelectorSuggestion.PdfSelectorSuggestionSanitizer.IsValidCssSelector(selector))
        {
            return new PdfSelectorSuggestionTestOutcome(false, null, "Invalid CSS selector.", null, null);
        }

        if (!Enum.TryParse<PdfSelectorExpectedAction>(expectedAction, true, out var action))
        {
            return new PdfSelectorSuggestionTestOutcome(false, null, "Unsupported expected action.", null, null);
        }

        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            var context = await browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true }).ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(pageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 }).ConfigureAwait(false);

            var locator = page.Locator(selector);
            if (await locator.CountAsync().ConfigureAwait(false) == 0)
            {
                return new PdfSelectorSuggestionTestOutcome(false, null, "Selector did not match any element.", null, null);
            }

            var target = locator.First;
            if (!await target.IsVisibleAsync().ConfigureAwait(false))
            {
                return new PdfSelectorSuggestionTestOutcome(false, null, "Selector matched a non-visible element.", null, null);
            }

            var resolvedUrl = action switch
            {
                PdfSelectorExpectedAction.ExtractHref => await ResolveHrefAsync(pageUrl, target).ConfigureAwait(false),
                PdfSelectorExpectedAction.InspectParentAnchor => await ResolveParentHrefAsync(pageUrl, target).ConfigureAwait(false),
                PdfSelectorExpectedAction.ClickAndWaitForPopup => await ResolveClickUrlAsync(page, target, waitForPopup: true).ConfigureAwait(false),
                PdfSelectorExpectedAction.ClickAndWaitForDownload => await ResolveClickUrlAsync(page, target, waitForPopup: false).ConfigureAwait(false),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(resolvedUrl) || !Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            {
                return new PdfSelectorSuggestionTestOutcome(false, resolvedUrl, "Could not resolve a PDF URL from the selector.", null, null);
            }

            var warmUp = Uri.TryCreate(pageUrl, UriKind.Absolute, out var warm) ? warm : null;
            var validation = await validator.ValidateAsync(
                uri,
                requirePdfContent,
                minimumSizeKb,
                useHeadlessBrowser,
                warmUp,
                cancellationToken).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                return new PdfSelectorSuggestionTestOutcome(
                    false,
                    uri.ToString(),
                    validation.FailureReason ?? "PDF validation failed.",
                    validation.SizeBytes,
                    validation.ContentType);
            }

            return new PdfSelectorSuggestionTestOutcome(
                true,
                uri.ToString(),
                null,
                validation.SizeBytes,
                validation.ContentType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Selector suggestion test failed for {Selector} on {Url}.", selector, pageUrl);
            return new PdfSelectorSuggestionTestOutcome(false, null, ex.Message, null, null);
        }
    }

    private static async Task<string?> ResolveHrefAsync(string pageUrl, ILocator target)
    {
        var href = await target.GetAttributeAsync("href").ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(href) ? null : new Uri(new Uri(pageUrl), href).ToString();
    }

    private static async Task<string?> ResolveParentHrefAsync(string pageUrl, ILocator target)
    {
        var href = await target.Locator("xpath=ancestor-or-self::a[1]").GetAttributeAsync("href").ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(href))
        {
            return new Uri(new Uri(pageUrl), href).ToString();
        }

        return await ResolveHrefAsync(pageUrl, target).ConfigureAwait(false);
    }

    private static async Task<string?> ResolveClickUrlAsync(IPage page, ILocator target, bool waitForPopup)
    {
        if (waitForPopup)
        {
            var popupTask = page.WaitForPopupAsync(new PageWaitForPopupOptions { Timeout = 15_000 });
            await target.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
            try
            {
                var popup = await popupTask.ConfigureAwait(false);
                return popup.Url;
            }
            catch (TimeoutException)
            {
                return page.Url;
            }
        }

        await target.ClickAsync(new LocatorClickOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        return page.Url;
    }
}
