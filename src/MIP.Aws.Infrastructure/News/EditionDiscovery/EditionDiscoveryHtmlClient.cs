using System.Net;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions.Browser;
using MIP.Aws.Infrastructure.News;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

public sealed class EditionDiscoveryHtmlClient(
    IHttpClientFactory httpClientFactory,
    IHeadlessBrowserService headless,
    ILogger<EditionDiscoveryHtmlClient> logger)
{
    private static readonly Regex HrefPdfRegex = new(@"https?://[^\s""'<>]+\.pdf", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<string> FetchHtmlAsync(Uri pageUri, bool useHeadlessFallback, CancellationToken cancellationToken)
    {
        // Al Ayam: HTTP-first — plain GET returns INAF links reliably; Playwright from datacenter often hits Cloudflare.
        if (pageUri.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase))
        {
            var html = await TryHttpAsync(pageUri, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html) && !PublisherAccessGuard.IsAccessBlocked(html))
            {
                return html;
            }

            if (useHeadlessFallback)
            {
                logger.LogInformation("Al Ayam HTTP fetch empty or blocked for {Url}; trying Playwright.", pageUri);
                var rendered = await headless.GetRenderedHtmlAsync(pageUri, TimeSpan.FromSeconds(120), cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(rendered))
                {
                    return rendered;
                }
            }

            return string.Empty;
        }

        var genericHtml = await TryHttpAsync(pageUri, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(genericHtml) && !PublisherAccessGuard.IsAccessBlocked(genericHtml))
        {
            return genericHtml;
        }

        if (!useHeadlessFallback)
        {
            return string.Empty;
        }

        logger.LogInformation("HTTP fetch empty or blocked for {Url}; trying Playwright.", pageUri);
        return await headless.GetRenderedHtmlAsync(pageUri, TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false)
               ?? string.Empty;
    }

    public static IReadOnlyList<string> ExtractPdfLinks(string html) =>
        HrefPdfRegex.Matches(html).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static int? MaxCapturedGroup(string html, string pattern)
    {
        var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
        int? max = null;
        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 2 || !int.TryParse(match.Groups[1].Value, out var value))
            {
                continue;
            }

            max = max is null || value > max ? value : max;
        }

        return max;
    }

    public static string? FirstGroup(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
    }

    private async Task<string> TryHttpAsync(Uri pageUri, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(EditionDiscoveryHtmlClient));
            using var response = await client.GetAsync(pageUri, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                return string.Empty;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HTTP fetch failed for {Url}", pageUri);
            return string.Empty;
        }
    }
}
