using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Ensures Bahrain - Al Ayam keeps a known-good PDF discovery configuration before each download.
/// Accepts both the original كل الصفحات selector and the successful INAF href + 300s wait settings.
/// </summary>
internal static class AlAyamSourceBaselineGuard
{
    public static async Task EnsureKnownGoodConfigurationAsync(
        IApplicationDbContext db,
        NewsSource source,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!AlAyamFullEditionPdf.UsesClickPath(source))
        {
            return;
        }

        if (!NeedsBaseline(source))
        {
            return;
        }

        source.Name = AlAyamPublicPdfBaseline.SourceName;
        source.BaseUrl = AlAyamPublicPdfBaseline.EpaperUrl;
        source.EditionUrl = AlAyamPublicPdfBaseline.EpaperUrl;
        source.PdfDiscoveryPageUrl = AlAyamPublicPdfBaseline.EpaperUrl;
        source.PdfLinkSelector = AlAyamPublicPdfBaseline.InafDirectPdfLinkSelector;
        source.UseHeadlessBrowser = true;
        source.DownloadWaitTimeoutSeconds = Math.Max(
            source.DownloadWaitTimeoutSeconds,
            AlAyamPublicPdfBaseline.ExtendedWaitTimeoutSeconds);
        source.ModifiedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Applied Al Ayam known-good baseline configuration before download (UseHeadlessBrowser=true, e-paper URLs restored).");
    }

    private static bool NeedsBaseline(NewsSource source) =>
        !source.UseHeadlessBrowser
        || !string.Equals(source.BaseUrl, AlAyamPublicPdfBaseline.EpaperUrl, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(source.EditionUrl, AlAyamPublicPdfBaseline.EpaperUrl, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(source.PdfDiscoveryPageUrl, AlAyamPublicPdfBaseline.EpaperUrl, StringComparison.OrdinalIgnoreCase)
        || !AlAyamPublicPdfBaseline.IsAcceptedPdfLinkSelector(source.PdfLinkSelector);
}
