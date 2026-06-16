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
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceId}): job status {Status}, trigger {Trigger}, correlation {CorrelationId}.",
                failedJob.Id,
                failedJob.NewsSourceId,
                failedJob.Status,
                failedJob.Trigger,
                failedJob.CorrelationId);
            return;
        }

        var settings = await settingsProvider.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceId}): auto AI recovery is disabled.",
                failedJob.Id,
                failedJob.NewsSourceId);
            return;
        }

        if (!AutoAiRecoveryEligibility.ShouldRunForTrigger(failedJob.Trigger, settings))
        {
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceId}): trigger {Trigger} is not enabled (RunAfterScheduledFailure={RunAfterScheduledFailure}, RunAfterManualFailure={RunAfterManualFailure}).",
                failedJob.Id,
                failedJob.NewsSourceId,
                failedJob.Trigger,
                settings.RunAfterScheduledFailure,
                settings.RunAfterManualFailure);
            return;
        }

        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == failedJob.NewsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId}: news source {SourceId} was not found.",
                failedJob.Id,
                failedJob.NewsSourceId);
            return;
        }

        if (!AutoAiRecoveryEligibility.IsSourceEnabled(source, settings.Enabled))
        {
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceName}): source auto AI recovery is disabled.",
                failedJob.Id,
                source.Name);
            return;
        }

        if (!AutoAiRecoveryEligibility.IsSourceTypeAllowed(source, settings))
        {
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceName}): source type {SourceType} is not eligible.",
                failedJob.Id,
                source.Name,
                source.SourceType);
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
            logger.LogInformation(
                "Skipped auto AI download recovery enqueue for job {JobId} (source {SourceName}): a recovery run is already queued.",
                failedJob.Id,
                source.Name);
            return;
        }

        var trigger = failedJob.Trigger == DownloadJobTrigger.Scheduled
            ? AutoAiRecoveryTrigger.ScheduledDownloadFailed
            : AutoAiRecoveryTrigger.ManualDownloadFailed;

        BackgroundJob.Enqueue<AutoAiDownloadRecoveryJob>(
            j => j.RunAsync(failedJob.NewsSourceId, failedJob.Id, trigger, CancellationToken.None));

        logger.LogInformation(
            "Queued auto AI download recovery for source {SourceId}, failed job {JobId}, trigger {Trigger}.",
            failedJob.NewsSourceId,
            failedJob.Id,
            trigger);
    }
}
