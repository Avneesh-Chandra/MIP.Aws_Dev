using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Ensures Bahrain - Al Ayam keeps known-good PDF discovery settings before each download attempt.
/// Prevents scheduled batches from running with stale/broken selectors that trigger Cloudflare failures.
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
        source.PdfLinkSelector = AlAyamPublicPdfBaseline.PdfLinkSelector;
        source.UseHeadlessBrowser = true;
        source.DownloadWaitTimeoutSeconds = Math.Max(source.DownloadWaitTimeoutSeconds, 180);
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
        || !string.Equals(source.PdfLinkSelector, AlAyamPublicPdfBaseline.PdfLinkSelector, StringComparison.Ordinal);
}
