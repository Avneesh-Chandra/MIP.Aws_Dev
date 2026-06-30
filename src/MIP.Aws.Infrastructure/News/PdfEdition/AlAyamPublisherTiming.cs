using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Al Ayam often publishes the daily INAF PDF link on the e-paper page minutes to hours after batch start.
/// Retries use delays for transient publisher/network blocks (not the primary batch-failure mode when another
/// datacenter egress IP can reach the same page at the same UTC time).
/// </summary>
internal static class AlAyamPublisherTiming
{
    internal static readonly TimeSpan[] HttpRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(45)
    ];

    internal static readonly TimeSpan AutoRecoveryWarmUp = TimeSpan.FromMinutes(2);

    internal static readonly TimeSpan[] DeferredDownloadDelays =
    [
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(60)
    ];

    internal const string DeferredCorrelationPrefix = "alayam-deferred:";

    internal static bool IsAlAyamSource(NewsSource source) =>
        AlAyamPublicPdfBaseline.IsSource(source)
        || AlAyamFullEditionPdf.UsesClickPath(source);

    internal static bool IsTransientPublisherFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return errorMessage.Contains("could not download a PDF from i.alayam.com", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("all-pages click path", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("bot protection", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("AccessBlocked", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("Response appears to be HTML", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("not a valid PDF", StringComparison.OrdinalIgnoreCase)
               || errorMessage.Contains("Missing PDF magic", StringComparison.OrdinalIgnoreCase);
    }

    internal static TimeSpan? ResolveDeferredDelay(int deferredAttemptsToday)
    {
        if (deferredAttemptsToday < 0 || deferredAttemptsToday >= DeferredDownloadDelays.Length)
        {
            return null;
        }

        return DeferredDownloadDelays[deferredAttemptsToday];
    }
}
