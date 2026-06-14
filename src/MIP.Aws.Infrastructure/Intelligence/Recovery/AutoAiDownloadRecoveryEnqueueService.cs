using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class AutoAiDownloadRecoveryEnqueueService(
    IApplicationDbContext db,
    AutoAiDownloadRecoverySettingsProvider settingsProvider,
    ILogger<AutoAiDownloadRecoveryEnqueueService> logger) : IAutoAiDownloadRecoveryEnqueueService
{
    public async Task TryEnqueueAfterFailureAsync(DownloadJob failedJob, CancellationToken cancellationToken)
    {
        if (!AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(failedJob))
        {
            return;
        }

        var settings = await settingsProvider.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return;
        }

        if (!AutoAiRecoveryEligibility.ShouldRunForTrigger(failedJob.Trigger, settings))
        {
            return;
        }

        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == failedJob.NewsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (source is null || !AutoAiRecoveryEligibility.IsSourceEnabled(source, settings.Enabled))
        {
            return;
        }

        if (!AutoAiRecoveryEligibility.IsSourceTypeAllowed(source, settings))
        {
            return;
        }

        var alreadyQueued = await db.AutoAiRecoveryRuns.AsNoTracking()
            .AnyAsync(
                r => !r.IsDeleted
                     && r.FailedDownloadJobId == failedJob.Id
                     && r.CompletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyQueued)
        {
            return;
        }

        var trigger = failedJob.Trigger == DownloadJobTrigger.Scheduled
            ? AutoAiRecoveryTrigger.ScheduledDownloadFailed
            : AutoAiRecoveryTrigger.ManualDownloadFailed;

        BackgroundJob.Enqueue<AutoAiDownloadRecoveryJob>(
            j => j.RunAsync(failedJob.NewsSourceId, failedJob.Id, trigger, CancellationToken.None));

        logger.LogInformation(
            "Queued auto AI download recovery for source {SourceId}, failed job {JobId}.",
            failedJob.NewsSourceId,
            failedJob.Id);
    }
}
