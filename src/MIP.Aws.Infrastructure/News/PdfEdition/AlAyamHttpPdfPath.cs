using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Fast HTTP discovery/download for Al Ayam (epaper HTML → INAF PDF on i.alayam.com).
/// Used before Playwright because datacenter browsers often hit Cloudflare while plain HTTP succeeds.
/// </summary>
internal static class AlAyamHttpPdfPath
{
    public static async Task<Uri?> TryDiscoverPdfUrlAsync(
        IHttpClientFactory httpClientFactory,
        NewsSource source,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var epaperUri = AlAyamFullEditionPdf.ResolveEpaperUri(source);
        var html = await FetchEpaperHtmlAsync(httpClientFactory, epaperUri, logger, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(html) || PublisherAccessGuard.IsAccessBlocked(html))
        {
            if (!string.IsNullOrWhiteSpace(html) && PublisherAccessGuard.IsAccessBlocked(html))
            {
                logger.LogWarning("Al Ayam HTTP epaper HTML looks blocked (bot protection) for {Source}", source.Name);
            }

            return TryResolveCachedPdfUrl(source);
        }

        var fromHtml = AlAyamFullEditionPdf.ExtractPdfUrlFromHtml(html);
        if (fromHtml is not null)
        {
            logger.LogInformation("Al Ayam PDF URL discovered via HTTP epaper HTML: {Url}", fromHtml);
            return fromHtml;
        }

        return TryResolveCachedPdfUrl(source);
    }

    public static async Task<byte[]?> TryDownloadBytesAsync(
        IHttpClientFactory httpClientFactory,
        NewsSource source,
        Uri? knownPdfUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pdfUrl = knownPdfUrl;
        if (pdfUrl is null || !IsAlAyamPdfHost(pdfUrl))
        {
            pdfUrl = await TryDiscoverPdfUrlAsync(httpClientFactory, source, logger, cancellationToken)
                .ConfigureAwait(false);
        }

        if (pdfUrl is null)
        {
            return null;
        }

        return await DownloadPdfViaHttpAsync(httpClientFactory, pdfUrl, logger, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Uri? TryResolveCachedPdfUrl(NewsSource source)
    {
        if (string.IsNullOrWhiteSpace(source.LastPdfUrl)
            || !Uri.TryCreate(source.LastPdfUrl.Trim(), UriKind.Absolute, out var cached)
            || !IsAlAyamPdfHost(cached))
        {
            return null;
        }

        return cached;
    }

    private static bool IsAlAyamPdfHost(Uri uri) =>
        uri.Host.Contains("i.alayam.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> FetchEpaperHtmlAsync(
        IHttpClientFactory httpClientFactory,
        Uri epaperUri,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(EditionDiscoveryHtmlClient));
            using var request = new HttpRequestMessage(HttpMethod.Get, epaperUri);
            request.Headers.Referrer = new Uri(AlAyamPublicPdfBaseline.EpaperUrl);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Al Ayam HTTP epaper fetch returned {Status} for {Url}", response.StatusCode, epaperUri);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Al Ayam HTTP epaper fetch failed for {Url}", epaperUri);
            return null;
        }
    }

    private static async Task<byte[]?> DownloadPdfViaHttpAsync(
        IHttpClientFactory httpClientFactory,
        Uri pdfUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(PdfEditionContentFetcher));
            using var request = new HttpRequestMessage(HttpMethod.Get, pdfUrl);
            request.Headers.Referrer = new Uri(AlAyamPublicPdfBaseline.EpaperUrl);

            using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Al Ayam HTTP PDF GET returned {Status} for {Url}", response.StatusCode, pdfUrl);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length > 0 && PdfEditionContentFetcher.IsPdf(bytes))
            {
                logger.LogInformation("Al Ayam PDF downloaded via HTTP ({Bytes} bytes, {Url})", bytes.Length, pdfUrl);
                return bytes;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Al Ayam HTTP PDF download failed for {Url}", pdfUrl);
        }

        return null;
    }
}
