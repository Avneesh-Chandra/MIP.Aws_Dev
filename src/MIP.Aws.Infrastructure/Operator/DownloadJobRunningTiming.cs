using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Shared stale-running thresholds for reconciliation and download-monitor display.</summary>
internal static class DownloadJobRunningTiming
{
    private static readonly TimeSpan StaleRunningJobThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PlaywrightPdfRunningJobThreshold = TimeSpan.FromMinutes(35);
    private static readonly TimeSpan RecoveryRunningJobThreshold = TimeSpan.FromMinutes(5);

    public static TimeSpan GetRunningJobAge(DownloadJob job)
    {
        var anchor = job.StartedAt ?? job.CreatedAt;
        var age = DateTimeOffset.UtcNow - anchor;
        if (age < TimeSpan.Zero)
        {
            age = DateTimeOffset.UtcNow - job.CreatedAt;
        }

        return age < TimeSpan.Zero ? TimeSpan.MaxValue : age;
    }

    public static TimeSpan ResolveRunningStaleThreshold(DownloadJob job, NewsSource? source) =>
        IsRecoveryDownloadJob(job)
            ? RecoveryRunningJobThreshold
            : source is not null && IsLongRunningPlaywrightSource(source)
                ? PlaywrightPdfRunningJobThreshold
                : StaleRunningJobThreshold;

    public static bool IsRunningJobStale(DownloadJob job, NewsSource? source) =>
        job.Status is DownloadJobStatus.Running or DownloadJobStatus.Pending
        && GetRunningJobAge(job) > ResolveRunningStaleThreshold(job, source);

    private static bool IsRecoveryDownloadJob(DownloadJob job) =>
        !string.IsNullOrWhiteSpace(job.CorrelationId)
        && job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase);

    private static bool IsLongRunningPlaywrightSource(NewsSource source) =>
        source.UseHeadlessBrowser
        && source.SourceType is NewsSourceType.PublicPdf
            or NewsSourceType.PublicHtml
            or NewsSourceType.WebPortalLogin;
}
