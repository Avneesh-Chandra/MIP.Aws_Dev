using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class AutoAiDownloadRecoveryOrchestrator(
    IApplicationDbContext db,
    ISourceRecoveryAnalysisService analysisService,
    ISourceRecoveryOrchestrator recoveryOrchestrator,
    IDownloadManager downloadManager,
    IAiRecoverySuggestionRanker ranker,
    AutoAiDownloadRecoverySettingsProvider settingsProvider,
    IAuditService audit,
    ILogger<AutoAiDownloadRecoveryOrchestrator> logger) : IAutoAiDownloadRecoveryOrchestrator
{
    public async Task<AutoAiRecoveryResultDto> RecoverAsync(
        Guid sourceId,
        Guid failedDownloadJobId,
        AutoAiRecoveryTrigger trigger,
        CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var actorUserId = settings.SystemActorUserId == Guid.Empty ? Guid.Empty : settings.SystemActorUserId;

        var job = await db.DownloadJobs
            .Include(j => j.NewsSource)
            .FirstOrDefaultAsync(j => j.Id == failedDownloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed download job was not found.");

        if (job.NewsSourceId != sourceId)
        {
            throw new InvalidOperationException("Download job does not belong to the specified source.");
        }

        var source = job.NewsSource;
        var run = new AutoAiRecoveryRun
        {
            Id = Guid.NewGuid(),
            NewsSourceId = sourceId,
            FailedDownloadJobId = failedDownloadJobId,
            Trigger = trigger,
            Status = AutoAiRecoveryRunStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AutoAiRecoveryRuns.Add(run);
        job.AutoAiRecoveryRunId = run.Id;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AutoAiRecoveryTimelineWriter.AddStep(run, "Original failure", job.ErrorMessage ?? "Download failed.", succeeded: false);

        if (!settings.Enabled || !AutoAiRecoveryEligibility.IsSourceEnabled(source, settings.Enabled))
        {
            return await CompleteSkippedAsync(run, job, AutoAiRecoveryRunStatus.SkippedIneligible, "Auto AI recovery is disabled.", cancellationToken).ConfigureAwait(false);
        }

        if (!AutoAiRecoveryEligibility.IsSourceTypeAllowed(source, settings))
        {
            return await CompleteSkippedAsync(run, job, AutoAiRecoveryRunStatus.SkippedIneligible, "Source type is not eligible for auto recovery.", cancellationToken).ConfigureAwait(false);
        }

        var auditRow = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == failedDownloadJobId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var failureType = SourceRecoveryFailureTypeMapper.Map(auditRow?.FailureCode, job.ErrorMessage);
        if (AutoAiRecoveryEligibility.RequiresManualIntervention(source, auditRow?.FailureCode, failureType))
        {
            job.Status = DownloadJobStatus.ManualInterventionRequired;
            return await CompleteSkippedAsync(run, job, AutoAiRecoveryRunStatus.SkippedIneligible, "Manual intervention required (compliance, credentials, MFA, or CAPTCHA).", cancellationToken).ConfigureAwait(false);
        }

        if (!await CheckLimitsAsync(sourceId, settings, cancellationToken).ConfigureAwait(false))
        {
            return await CompleteSkippedAsync(run, job, AutoAiRecoveryRunStatus.SkippedCooldown, "Cooldown or daily auto-recovery limit reached.", cancellationToken).ConfigureAwait(false);
        }

        await audit.RecordAdminActionAsync(
            AutoAiRecoveryAuditEvents.Started,
            "AutoAiRecoveryRun",
            run.Id.ToString(),
            new { sourceId, failedDownloadJobId, trigger },
            cancellationToken).ConfigureAwait(false);

        run.Status = AutoAiRecoveryRunStatus.Analyzing;
        job.Status = DownloadJobStatus.AutoAiRecoveryAnalyzing;
        AutoAiRecoveryTimelineWriter.AddStep(run, "AI analysis started", "Running source recovery analysis.");
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        SourceRecoveryAnalysisDto analysis;
        try
        {
            analysis = await analysisService.AnalyzeAndPersistAsync(failedDownloadJobId, actorUserId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto AI recovery analysis failed for source {SourceId}.", sourceId);
            return await CompleteFailureAsync(run, job, $"AI analysis failed: {ex.Message}", cancellationToken).ConfigureAwait(false);
        }

        var attempt = await db.SourceRecoveryAttempts
            .FirstAsync(a => a.Id == analysis.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        attempt.IsAutomatic = true;
        attempt.AutoAiRecoveryRunId = run.Id;
        run.SourceRecoveryAttemptId = attempt.Id;

        var ranked = ranker.RankForAutoRecovery(analysis.Options, settings);
        await audit.RecordAdminActionAsync(
            AutoAiRecoveryAuditEvents.SuggestionRanked,
            "AutoAiRecoveryRun",
            run.Id.ToString(),
            new { rankedCount = ranked.Count },
            cancellationToken).ConfigureAwait(false);

        if (ranked.Count == 0)
        {
            return await CompleteSkippedAsync(
                run,
                job,
                AutoAiRecoveryRunStatus.SkippedNoSuggestions,
                "No safe AI recovery suggestions met confidence and risk thresholds.",
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var option in ranked)
        {
            if (!AutoAiRecoveryPatchValidator.IsOptionSafeForAutoApply(option, settings, out var rejectReason))
            {
                await audit.RecordAdminActionAsync(
                    AutoAiRecoveryAuditEvents.SuggestionSkippedUnsafe,
                    "AutoAiRecoveryRun",
                    run.Id.ToString(),
                    new { option.Title, rejectReason },
                    cancellationToken).ConfigureAwait(false);
                AutoAiRecoveryTimelineWriter.AddStep(run, "Suggestion skipped", rejectReason ?? option.Title, succeeded: false);
                continue;
            }

            run.Status = AutoAiRecoveryRunStatus.ApplyingCandidate;
            run.SuggestionsTried++;
            job.Status = DownloadJobStatus.AutoAiRecoveryApplying;
            AutoAiRecoveryTimelineWriter.AddStep(run, $"Suggestion {option.OptionIndex} applied", option.Title);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            SourceRecoveryApplyResultDto applyResult;
            try
            {
                applyResult = await recoveryOrchestrator.ApplyAndRetryAsync(
                    attempt.Id,
                    option.OptionIndex,
                    actorUserId,
                    isAdmin: true,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auto AI recovery could not apply option {Index} for attempt {AttemptId}.", option.OptionIndex, attempt.Id);
                AutoAiRecoveryTimelineWriter.AddStep(run, "Apply failed", ex.Message, succeeded: false);
                continue;
            }

            if (applyResult.Status == SourceRecoveryAttemptStatus.PendingAdminApproval)
            {
                AutoAiRecoveryTimelineWriter.AddStep(run, "Suggestion requires admin approval", option.Title, succeeded: false);
                continue;
            }

            await audit.RecordAdminActionAsync(
                AutoAiRecoveryAuditEvents.PatchApplied,
                "SourceRecoveryAttempt",
                attempt.Id.ToString(),
                new { option.Title, option.ConfidenceScore },
                cancellationToken).ConfigureAwait(false);

            var retryJobId = attempt.RetryDownloadJobId
                             ?? throw new InvalidOperationException("Recovery retry job was not created.");

            run.Status = AutoAiRecoveryRunStatus.RetryingDownload;
            run.RetryDownloadJobId = retryJobId;
            job.Status = DownloadJobStatus.AutoAiRecoveryRetrying;
            AutoAiRecoveryTimelineWriter.AddStep(run, "Retry started", $"Retry job {retryJobId:N}");
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await audit.RecordAdminActionAsync(
                AutoAiRecoveryAuditEvents.RetryStarted,
                "DownloadJob",
                retryJobId.ToString(),
                new { attemptId = attempt.Id },
                cancellationToken).ConfigureAwait(false);

            using (DownloadExecutionContext.UseTrigger(DownloadJobTrigger.AutoAiRecovery))
            {
                await downloadManager.ExecuteDownloadJobAsync(retryJobId, cancellationToken).ConfigureAwait(false);
            }

            var finalize = await recoveryOrchestrator.FinalizeAttemptAsync(attempt.Id, cancellationToken).ConfigureAwait(false);
            var retryJob = await db.DownloadJobs.AsNoTracking()
                .FirstAsync(j => j.Id == retryJobId, cancellationToken)
                .ConfigureAwait(false);

            attempt = await db.SourceRecoveryAttempts
                .FirstAsync(a => a.Id == attempt.Id, cancellationToken)
                .ConfigureAwait(false);

            if (finalize.Status == SourceRecoveryAttemptStatus.Succeeded && await ValidateRetrySuccessAsync(retryJob, source, cancellationToken).ConfigureAwait(false))
            {
                run.Status = AutoAiRecoveryRunStatus.CompletedSuccess;
                run.SuccessfulOptionIndex = option.OptionIndex;
                run.SuccessfulOptionTitle = option.Title;
                run.SuccessfulCandidateVersionId = finalize.CandidateVersionId;
                run.ResultSummary = "Download retry succeeded; candidate configuration activated.";
                run.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = DownloadJobStatus.SuccessWithAutoAiRecovery;

                var mutableRetry = await db.DownloadJobs
                    .FirstAsync(j => j.Id == retryJobId, cancellationToken)
                    .ConfigureAwait(false);
                mutableRetry.Status = DownloadJobStatus.SuccessWithAutoAiRecovery;
                mutableRetry.Trigger = DownloadJobTrigger.AutoAiRecovery;

                AutoAiRecoveryTimelineWriter.AddStep(run, "Retry succeeded", option.Title);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await audit.RecordAdminActionAsync(
                    AutoAiRecoveryAuditEvents.CompletedSuccess,
                    "AutoAiRecoveryRun",
                    run.Id.ToString(),
                    new { option.Title, retryJobId },
                    cancellationToken).ConfigureAwait(false);

                return ToResult(run, true);
            }

            AutoAiRecoveryTimelineWriter.AddStep(
                run,
                "Retry failed",
                retryJob.ErrorMessage ?? finalize.Message ?? "Retry failed.",
                succeeded: false);
            run.Status = AutoAiRecoveryRunStatus.CandidateFailed;
            await audit.RecordAdminActionAsync(
                AutoAiRecoveryAuditEvents.RetryFailed,
                "AutoAiRecoveryRun",
                run.Id.ToString(),
                new { option.Title, retryJob.ErrorMessage },
                cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await CompleteFailureAsync(run, job, "All safe AI recovery suggestions were tried without success.", cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ValidateRetrySuccessAsync(
        DownloadJob retryJob,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        if (retryJob.Status != DownloadJobStatus.Succeeded)
        {
            return false;
        }

        var file = await db.DownloadedFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && f.DownloadJobId == retryJob.Id)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (file is null)
        {
            var pdfRow = await db.PdfEditionDownloads.AsNoTracking()
                .Where(p => !p.IsDeleted && p.DownloadJobId == retryJob.Id && p.Status == PdfEditionStatus.Downloaded)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (pdfRow?.DownloadedFileId is null)
            {
                return false;
            }

            file = await db.DownloadedFiles.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == pdfRow.DownloadedFileId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (file is null || string.IsNullOrWhiteSpace(file.BlobUri))
        {
            return false;
        }

        var minBytes = Math.Max(1, source.MinimumPdfSizeKb) * 1024L;
        if (file.SizeBytes < minBytes)
        {
            return false;
        }

        return file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
               || file.OriginalUrl.Contains(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CheckLimitsAsync(Guid sourceId, AutoAiDownloadRecoveryOptions settings, CancellationToken cancellationToken)
    {
        var dayStart = DateTimeOffset.UtcNow.Date;
        var attemptsToday = await db.AutoAiRecoveryRuns.AsNoTracking()
            .CountAsync(
                r => !r.IsDeleted
                     && r.NewsSourceId == sourceId
                     && r.CreatedAt >= dayStart
                     && r.Status != AutoAiRecoveryRunStatus.SkippedIneligible,
                cancellationToken)
            .ConfigureAwait(false);

        if (attemptsToday >= settings.MaxAutoRecoveryAttemptsPerDayPerSource)
        {
            return false;
        }

        var cooldownSince = DateTimeOffset.UtcNow.AddMinutes(-settings.CooldownMinutesPerSource);
        var recent = await db.AutoAiRecoveryRuns.AsNoTracking()
            .AnyAsync(
                r => !r.IsDeleted
                     && r.NewsSourceId == sourceId
                     && r.CreatedAt >= cooldownSince
                     && (r.Status == AutoAiRecoveryRunStatus.CompletedSuccess
                         || r.Status == AutoAiRecoveryRunStatus.Analyzing
                         || r.Status == AutoAiRecoveryRunStatus.RetryingDownload
                         || r.Status == AutoAiRecoveryRunStatus.ApplyingCandidate),
                cancellationToken)
            .ConfigureAwait(false);

        return !recent;
    }

    private async Task<AutoAiRecoveryResultDto> CompleteSkippedAsync(
        AutoAiRecoveryRun run,
        DownloadJob job,
        AutoAiRecoveryRunStatus status,
        string summary,
        CancellationToken cancellationToken)
    {
        run.Status = status;
        run.ResultSummary = summary;
        run.CompletedAt = DateTimeOffset.UtcNow;
        if (job.Status == DownloadJobStatus.Failed)
        {
            job.Status = DownloadJobStatus.AutoAiRecoverySkipped;
        }

        AutoAiRecoveryTimelineWriter.AddStep(run, "Skipped", summary, succeeded: false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(run, false);
    }

    private async Task<AutoAiRecoveryResultDto> CompleteFailureAsync(
        AutoAiRecoveryRun run,
        DownloadJob job,
        string summary,
        CancellationToken cancellationToken)
    {
        run.Status = AutoAiRecoveryRunStatus.CompletedFailure;
        run.ResultSummary = summary;
        run.CompletedAt = DateTimeOffset.UtcNow;
        job.Status = DownloadJobStatus.FailedAfterAutoAiRecovery;
        AutoAiRecoveryTimelineWriter.AddStep(run, "Final result", summary, succeeded: false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await CreateAdminNotificationAsync(run, job, summary, cancellationToken).ConfigureAwait(false);

        await audit.RecordAdminActionAsync(
            AutoAiRecoveryAuditEvents.CompletedFailure,
            "AutoAiRecoveryRun",
            run.Id.ToString(),
            new { summary, run.SuggestionsTried },
            cancellationToken).ConfigureAwait(false);

        return ToResult(run, false);
    }

    private async Task CreateAdminNotificationAsync(
        AutoAiRecoveryRun run,
        DownloadJob job,
        string summary,
        CancellationToken cancellationToken)
    {
        var exists = await db.AdminInterventionNotifications.AsNoTracking()
            .AnyAsync(
                n => !n.IsDeleted && n.DownloadJobId == job.Id && n.Status != AdminInterventionNotificationStatus.Resolved,
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        db.AdminInterventionNotifications.Add(new AdminInterventionNotification
        {
            Id = Guid.NewGuid(),
            NewsSourceId = run.NewsSourceId,
            DownloadJobId = job.Id,
            FailureReason = $"Auto AI recovery failed after {run.SuggestionsTried} suggestion(s). {summary}",
            SuggestedAction = "Review failure details, recovery timeline, and apply a manual AI recovery fix or update source configuration.",
            Status = AdminInterventionNotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static AutoAiRecoveryResultDto ToResult(AutoAiRecoveryRun run, bool succeeded) =>
        new(
            run.Id,
            run.NewsSourceId,
            run.FailedDownloadJobId,
            run.Status,
            succeeded,
            run.ResultSummary ?? string.Empty,
            run.SuggestionsTried,
            run.SuccessfulOptionTitle,
            run.RetryDownloadJobId,
            AutoAiRecoveryTimelineJson.Deserialize(run.TimelineJson));
}
