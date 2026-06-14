using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

/// <summary>
/// Full-edition PDF from alayam.com/epaper (INAF_* all-pages download link).
/// </summary>
public sealed class AlAyamEditionDiscovery(
    EditionDiscoveryHtmlClient html,
    ILogger<AlAyamEditionDiscovery> logger) : IEditionUrlDiscovery
{
    public const string Key = "news.alayam";

    private static readonly Regex AllPagesPdfRegex = new(
        @"id=""aPDFdownloadAllPages""[^>]*href=""(?<url>https://i\.alayam\.com/[^""]+\.pdf)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InafPdfRegex = new(
        @"https://i\.alayam\.com/ayamnewsa/upload/issue/\d+/\d+/PDF/INAF_[^""]+\.pdf",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string ConnectorKey => Key;

    public async Task<EditionDiscoveryResult> DiscoverLatestEditionAsync(NewsSource source, CancellationToken cancellationToken)
    {
        // Prefer the known e-paper landing page even when legacy rows have a directory-style EditionUrl.
        var epaperUri = ResolveEpaperPageUri(source);

        var content = await html.FetchHtmlAsync(epaperUri, source.UseHeadlessBrowser, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Could not load Al Ayam e-paper: {epaperUri}");
        }

        if (PublisherAccessGuard.IsAccessBlocked(content))
        {
            throw new InvalidOperationException("Publisher blocked automated access (Cloudflare/bot protection) on Al Ayam e-paper page.");
        }

        var match = AllPagesPdfRegex.Match(content);
        string? pdfUrl = match.Success ? match.Groups["url"].Value : null;
        if (string.IsNullOrWhiteSpace(pdfUrl))
        {
            var inaf = InafPdfRegex.Match(content);
            pdfUrl = inaf.Success ? inaf.Value : null;
        }

        if (string.IsNullOrWhiteSpace(pdfUrl) || !Uri.TryCreate(pdfUrl, UriKind.Absolute, out var pdfUri))
        {
            throw new InvalidOperationException("No Al Ayam full-edition PDF link found on e-paper page.");
        }

        logger.LogInformation("Al Ayam latest edition PDF -> {Url}", pdfUri);
        return new EditionDiscoveryResult(pdfUri, "application/pdf", source.Name);
    }

    private static Uri ResolveEpaperPageUri(NewsSource source)
    {
        if (Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri)
            && baseUri.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
            && baseUri.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }

        if (Uri.TryCreate(source.EditionUrl, UriKind.Absolute, out var editionUri)
            && editionUri.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase)
            && editionUri.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase))
        {
            return editionUri;
        }

        return new Uri("https://www.alayam.com/epaper");
    }
}
