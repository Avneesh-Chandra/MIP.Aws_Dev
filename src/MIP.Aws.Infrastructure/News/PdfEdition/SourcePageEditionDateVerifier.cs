using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.Browser;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class SourcePageEditionDateVerifier(ILogger<SourcePageEditionDateVerifier> logger)
    : ISourcePageEditionDateVerifier
{
    public bool IsSupported(NewsSource source) => SourcePageEditionDateProfiles.IsSupported(source);

    public Task<SourcePageEditionDateCheck> VerifyFromPageAsync(
        IPage page,
        NewsSource source,
        DateOnly expectedEditionDate,
        CancellationToken cancellationToken) =>
        SourcePageEditionDatePageVerifier.VerifyAsync(page, source, expectedEditionDate, cancellationToken);

    public async Task<SourcePageEditionDateCheck> VerifyByNavigationAsync(
        NewsSource source,
        Uri? pageUrl,
        DateOnly expectedEditionDate,
        CancellationToken cancellationToken)
    {
        if (!IsSupported(source))
        {
            return SourcePageEditionDateCheck.Skipped(expectedEditionDate);
        }

        var target = pageUrl ?? SourcePageEditionDateProfiles.ResolveVerificationUrl(source);
        if (target is null)
        {
            return SourcePageEditionDateCheck.Unparseable(expectedEditionDate, null);
        }

        var timeoutMs = Math.Clamp(source.DownloadWaitTimeoutSeconds, 30, 180) * 1000;
        try
        {
            using var playwright = await PlaywrightBrowserLaunch.CreatePlaywrightAsync().ConfigureAwait(false);
            await using var browser = await PlaywrightBrowserLaunch.LaunchChromiumAsync(playwright).ConfigureAwait(false);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = SourcePageEditionDateProfiles.ResolveLocale(source),
                AcceptDownloads = true
            }).ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(timeoutMs);
            await page.GotoAsync(
                target.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutMs })
                .ConfigureAwait(false);

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Header text is usually available after DOMContentLoaded.
            }

            return await SourcePageEditionDatePageVerifier.VerifyAsync(page, source, expectedEditionDate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Source page edition date verification failed for {Source}", source.Name);
            return new SourcePageEditionDateCheck(
                true,
                false,
                expectedEditionDate,
                null,
                null,
                $"Could not open the source page to verify today's edition date: {ex.Message}");
        }
    }
}

public static class SourcePageEditionDatePageVerifier
{
    public static async Task<SourcePageEditionDateCheck> VerifyAsync(
        IPage page,
        NewsSource source,
        DateOnly expectedEditionDate,
        CancellationToken cancellationToken)
    {
        if (!SourcePageEditionDateProfiles.IsSupported(source))
        {
            return SourcePageEditionDateCheck.Skipped(expectedEditionDate);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var text = await ExtractEditionTextAsync(page, source, cancellationToken).ConfigureAwait(false);
        if (!ArabicGregorianDateParser.TryParseFirst(text, out var parsed, out var snippet))
        {
            return SourcePageEditionDateCheck.Unparseable(expectedEditionDate, snippet ?? Trim(text));
        }

        return parsed == expectedEditionDate
            ? SourcePageEditionDateCheck.Matched(expectedEditionDate, parsed, snippet)
            : SourcePageEditionDateCheck.Mismatch(expectedEditionDate, parsed, snippet);
    }

    private static async Task<string?> ExtractEditionTextAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        foreach (var selector in SourcePageEditionDateProfiles.SelectorsFor(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var locator = page.Locator(selector);
                var count = await locator.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < Math.Min(count, 4); i++)
                {
                    var text = await locator.Nth(i).InnerTextAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch
            {
                // try next selector
            }
        }

        try
        {
            var header = page.Locator("header, [role='banner'], nav").First;
            if (await header.CountAsync().ConfigureAwait(false) > 0)
            {
                var headerText = await header.InnerTextAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(headerText))
                {
                    return headerText;
                }
            }
        }
        catch
        {
            // fall through
        }

        if (SourcePageEditionDateProfiles.IsPressReaderSource(source))
        {
            try
            {
                var title = await page.TitleAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                var headerText = await page.EvaluateAsync<string>(
                        @"() => {
                            const nav = document.querySelector('header, nav, [role=""banner""]');
                            return nav ? nav.innerText : (document.body?.innerText ?? '').slice(0, 4000);
                        }")
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(headerText))
                {
                    return headerText;
                }
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            return await page.Locator("body").InnerTextAsync().ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static string? Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= 160 ? trimmed : trimmed[..160] + "…";
    }
}

internal static class SourcePageEditionDateProfiles
{
    public static bool IsSupported(NewsSource source)
    {
        var key = source.ConnectorKey ?? string.Empty;
        if (key is AkhbarAlKhaleejEditionDiscovery.Key
            or AawsatEditionDiscovery.Key
            or AlAyamEditionDiscovery.Key
            or DarAlKhaleejPressReaderBaseline.ConnectorKey)
        {
            return true;
        }

        var host = HostFrom(source);
        return host.Contains("akhbar-alkhaleej.com", StringComparison.OrdinalIgnoreCase)
               || host.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase)
               || host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
               || host.Contains("pressreader.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPressReaderSource(NewsSource source) =>
        string.Equals(source.ConnectorKey, DarAlKhaleejPressReaderBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase)
        || HostFrom(source).Contains("pressreader.com", StringComparison.OrdinalIgnoreCase);

    public static Uri? ResolveVerificationUrl(NewsSource source)
    {
        foreach (var raw in new[] { source.PdfDiscoveryPageUrl, source.EditionUrl, source.BaseUrl })
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return source.ConnectorKey switch
        {
            AkhbarAlKhaleejEditionDiscovery.Key => new Uri("https://akhbar-alkhaleej.com/"),
            AawsatEditionDiscovery.Key => new Uri("https://aawsat.com/"),
            AlAyamEditionDiscovery.Key => new Uri(AlAyamFullEditionPdf.EpaperUrl),
            DarAlKhaleejPressReaderBaseline.ConnectorKey when !string.IsNullOrWhiteSpace(source.EditionUrl)
                && Uri.TryCreate(source.EditionUrl.Trim(), UriKind.Absolute, out var editionUri) => editionUri,
            _ => null
        };
    }

    public static string ResolveLocale(NewsSource source) =>
        source.ConnectorKey switch
        {
            DarAlKhaleejPressReaderBaseline.ConnectorKey => "en-US",
            AawsatEditionDiscovery.Key => "ar-SA",
            _ => "ar-BH"
        };

    public static IReadOnlyList<string> SelectorsFor(NewsSource source)
    {
        var key = source.ConnectorKey ?? string.Empty;
        if (key == AkhbarAlKhaleejEditionDiscovery.Key
            || HostFrom(source).Contains("akhbar-alkhaleej.com", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "h3:has-text('العدد')",
                "text=/العدد\\s*:/",
                "header",
                "[role='banner']"
            ];
        }

        if (key == AawsatEditionDiscovery.Key
            || HostFrom(source).Contains("aawsat.com", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "header",
                "[role='banner']",
                "text=/يونيو|يناير|فبراير|مارس|أبريل|مايو|يوليو|أغسطس|سبتمبر|أكتوبر|نوفمبر|ديسمبر/",
                "text=/Jun|Jan|Feb|Mar|Apr|May|Jul|Aug|Sep|Oct|Nov|Dec/"
            ];
        }

        if (key == AlAyamEditionDiscovery.Key
            || HostFrom(source).Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "text=/العدد/",
                "header",
                "[role='banner']",
                ".navbar",
                "nav"
            ];
        }

        if (key == DarAlKhaleejPressReaderBaseline.ConnectorKey
            || HostFrom(source).Contains("pressreader.com", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "text=/\\d{1,2}\\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\\s+\\d{4}/",
                "[class*='issue-date']",
                "[class*='IssueDate']",
                "header",
                "[role='banner']",
                "nav"
            ];
        }

        return ["header", "[role='banner']", "nav"];
    }

    private static string HostFrom(NewsSource source)
    {
        foreach (var raw in new[] { source.EditionUrl, source.BaseUrl, source.PdfDiscoveryPageUrl })
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }

        return string.Empty;
    }
}
