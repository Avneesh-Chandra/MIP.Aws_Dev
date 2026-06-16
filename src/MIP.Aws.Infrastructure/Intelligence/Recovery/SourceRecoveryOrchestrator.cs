using System.Text.Json;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Jobs;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Operator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class SourceRecoveryOrchestrator(
    IApplicationDbContext db,
    INewsDownloadJobScheduler scheduler,
    IAuditService audit,
    IAutoAiDownloadRecoveryEnqueueService autoAiRecoveryEnqueue,
    IOptions<AiSourceRecoveryOptions> recoveryOptions,
    ILogger<SourceRecoveryOrchestrator> logger) : ISourceRecoveryOrchestrator
{
    public async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        await DownloadJobReconciliation.ReconcileStaleJobsAsync(
                db,
                this,
                autoAiRecoveryEnqueue,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
        await ReconcileUnfinalizedAttemptsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SourceRecoveryPreviewDto> PreviewChangesAsync(
        Guid attemptId,
        int optionIndex,
        CancellationToken cancellationToken)
    {
        var (attempt, option) = await LoadAttemptOptionAsync(attemptId, optionIndex, cancellationToken)
            .ConfigureAwait(false);
        var source = await db.NewsSources.AsNoTracking()
            .FirstAsync(s => s.Id == attempt.NewsSourceId, cancellationToken)
            .ConfigureAwait(false);
        var current = SourceRecoveryConfigurationSnapshot.FromEntity(source);
        var patch = option.Patch;
        var changes = BuildChangeList(current.ToPatch(), patch);
        return new SourceRecoveryPreviewDto(patch, changes);
    }

    public async Task<SourceRecoveryApplyResultDto> ApplyAndRetryAsync(
        Guid attemptId,
        int optionIndex,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var (attempt, option) = await LoadAttemptOptionAsync(attemptId, optionIndex, cancellationToken)
            .ConfigureAwait(false);
        EnsureAttemptCanBeApplied(attempt);

        if (!isAdmin)
        {
            var allowedRisks = recoveryOptions.Value.OperatorAllowedRiskLevels
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => r.ToLowerInvariant())
                .ToHashSet();

            var risk = option.RiskLevel switch
            {
                SourceRecoveryRiskLevel.Low => "low",
                SourceRecoveryRiskLevel.High => "high",
                _ => "medium"
            };

            if (!allowedRisks.Contains(risk)
                || option.ConfidenceScore < recoveryOptions.Value.OperatorAutoApplyConfidenceThreshold)
            {
                attempt.Status = SourceRecoveryAttemptStatus.PendingAdminApproval;
                attempt.SelectedOptionIndex = optionIndex;
                await SaveChangesWithSourceConcurrencyRetryAsync(option.Patch, cancellationToken).ConfigureAwait(false);
                return new SourceRecoveryApplyResultDto(
                    attempt.Id,
                    Guid.Empty,
                    null,
                    attempt.Status,
                    "This fix requires admin approval due to risk or confidence threshold.");
            }
        }

        var source = await ReloadMutableSourceAsync(attempt.NewsSourceId, cancellationToken).ConfigureAwait(false);
        if (!IsRecoveryEligibleSource(source))
        {
            throw new InvalidOperationException(
                $"AI recovery apply/retry requires WebPortalLogin or a PDF-discovery source (PublicHtml/PublicPdf with PdfDiscoveryEnabled). {source.Name} is configured as {source.SourceType}.");
        }

        var currentSnapshot = SourceRecoveryConfigurationSnapshot.FromEntity(source);
        var nextVersion = await GetNextVersionNumberAsync(source.Id, cancellationToken).ConfigureAwait(false);

        var rollbackVersion = new SourceConfigurationVersion
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            VersionNumber = nextVersion,
            Reason = "Pre-recovery rollback snapshot",
            JsonConfiguration = currentSnapshot.ToJson(),
            Status = SourceConfigurationVersionStatus.Rollback,
            CreatedByUserId = actorUserId,
            SourceRecoveryAttemptId = attempt.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        currentSnapshot.ApplyPatch(option.Patch, source);
        SourceRecoveryPublisherDefaults.ApplyAfterPatch(source);

        var candidateVersion = new SourceConfigurationVersion
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            VersionNumber = nextVersion + 1,
            Reason = $"AI recovery: {option.Title}",
            JsonConfiguration = SourceRecoveryConfigurationSnapshot.FromEntity(source).ToJson(),
            Status = SourceConfigurationVersionStatus.Candidate,
            CreatedByUserId = actorUserId,
            SourceRecoveryAttemptId = attempt.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.SourceConfigurationVersions.Add(rollbackVersion);
        db.SourceConfigurationVersions.Add(candidateVersion);

        source.ModifiedAt = DateTimeOffset.UtcNow;

        var previousFailures = await db.DownloadJobs.AsNoTracking()
            .CountAsync(j => j.NewsSourceId == source.Id && j.Status == DownloadJobStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        var retryJob = new DownloadJob
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            Status = DownloadJobStatus.Pending,
            CorrelationId = $"recovery:{attempt.Id:N}",
            Trigger = Domain.Enums.DownloadJobTrigger.Recovery,
            CreatedAt = DateTimeOffset.UtcNow,
            RetryCount = previousFailures,
            RobotsTxtAllowed = true
        };
        db.DownloadJobs.Add(retryJob);
        var applyPersistence = new ApplyAttemptPersistence(
            optionIndex,
            actorUserId,
            option,
            candidateVersion.Id,
            rollbackVersion.Id,
            retryJob.Id);
        ApplyAttemptMutations(
            attempt,
            applyPersistence.OptionIndex,
            applyPersistence.ActorUserId,
            applyPersistence.Option,
            applyPersistence.CandidateVersionId,
            applyPersistence.RollbackVersionId,
            applyPersistence.RetryJobId,
            SourceRecoveryAttemptStatus.RetryEnqueued);
        await SaveChangesWithApplyConcurrencyRetryAsync(applyPersistence, option.Patch, cancellationToken)
            .ConfigureAwait(false);
        await TryHealOrphanedApplyAsync(attempt, cancellationToken).ConfigureAwait(false);

        scheduler.EnqueueDownloadJob(retryJob.Id);

        await audit.RecordAdminActionAsync(
            SourceRecoveryAuditEvents.SuggestionApplied,
            "SourceRecoveryAttempt",
            attempt.Id.ToString(),
            new { option.Title, option.ConfidenceScore },
            cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            SourceRecoveryAuditEvents.RetryStarted,
            "NewsSource",
            source.Id.ToString(),
            new { attemptId = attempt.Id },
            cancellationToken).ConfigureAwait(false);

        var persistedStatus = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => a.Id == attempt.Id)
            .Select(a => a.Status)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SourceRecoveryApplyResultDto(
            attempt.Id,
            candidateVersion.Id,
            rollbackVersion.Id,
            persistedStatus,
            "Configuration applied and download retry enqueued.");
    }

    public async Task<SourceRecoveryApplyResultDto> FinalizeAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        DetachTrackedEntity<SourceRecoveryAttempt>(attemptId);

        var attempt = await db.SourceRecoveryAttempts
            .Include(a => a.NewsSource)
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recovery attempt not found.");

        await TryHealOrphanedApplyAsync(attempt, cancellationToken).ConfigureAwait(false);

        if (attempt.AppliedAt is null)
        {
            throw new InvalidOperationException("Recovery attempt has not been applied yet.");
        }

        if (attempt.Status is SourceRecoveryAttemptStatus.Succeeded
            or SourceRecoveryAttemptStatus.Failed
            or SourceRecoveryAttemptStatus.RolledBack)
        {
            return ToApplyResult(attempt);
        }

        var jobQuery = db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.NewsSourceId == attempt.NewsSourceId);

        var job = attempt.RetryDownloadJobId is Guid retryJobId
            ? await jobQuery.FirstOrDefaultAsync(j => j.Id == retryJobId, cancellationToken).ConfigureAwait(false)
            : await jobQuery
                .Where(j => j.CreatedAt >= attempt.AppliedAt)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        if (job is null || IsRetryStillInProgress(job.Status))
        {
            return new SourceRecoveryApplyResultDto(
                attempt.Id,
                attempt.CandidateVersionId ?? Guid.Empty,
                attempt.RollbackVersionId,
                attempt.Status,
                "Retry is still in progress.");
        }

        attempt.NewsSource = await ReloadMutableSourceAsync(attempt.NewsSourceId, cancellationToken).ConfigureAwait(false);

        if (IsRetrySuccessful(job.Status))
        {
            await ActivateCandidateAsync(attempt, cancellationToken).ConfigureAwait(false);
            await TryRecordKnowledgeAsync(attempt, success: true, cancellationToken).ConfigureAwait(false);
            attempt.Status = SourceRecoveryAttemptStatus.Succeeded;
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            attempt.ActualSuccessPercent = 100;
            attempt.ResultSummary = "Download retry succeeded; candidate configuration activated.";
            await SaveChangesWithRecoveryConcurrencyRetryAsync(cancellationToken).ConfigureAwait(false);

            await audit.RecordAdminActionAsync(
                SourceRecoveryAuditEvents.RetrySucceeded,
                "SourceRecoveryAttempt",
                attempt.Id.ToString(),
                new { job.Id },
                cancellationToken).ConfigureAwait(false);

            return ToApplyResult(attempt);
        }

        var selectedOption = GetSelectedOption(attempt);
        var retainPublisherBaseline = PublisherRecoveryBaseline.ShouldRetainConfigAfterFailedRetry(
            attempt.NewsSource,
            selectedOption);

        if (retainPublisherBaseline)
        {
            await ActivateCandidateAsync(attempt, cancellationToken).ConfigureAwait(false);
            await TryRecordKnowledgeAsync(attempt, success: false, cancellationToken).ConfigureAwait(false);
            attempt.Status = SourceRecoveryAttemptStatus.Failed;
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            attempt.ActualSuccessPercent = 0;
            var failureSummary = await BuildRetryFailureSummaryAsync(job, cancellationToken).ConfigureAwait(false);
            attempt.ResultSummary =
                $"{failureSummary} Publisher baseline configuration kept active for subsequent scheduled downloads.";
            await SaveChangesWithRecoveryConcurrencyRetryAsync(cancellationToken).ConfigureAwait(false);

            await audit.RecordAdminActionAsync(
                SourceRecoveryAuditEvents.RetryFailed,
                "SourceRecoveryAttempt",
                attempt.Id.ToString(),
                new { job.Id, job.ErrorMessage, retainedConfig = true },
                cancellationToken).ConfigureAwait(false);

            return ToApplyResult(attempt);
        }

        await RollbackInternalAsync(attempt, "Retry failed after AI recovery apply.", cancellationToken).ConfigureAwait(false);
        await TryRecordKnowledgeAsync(attempt, success: false, cancellationToken).ConfigureAwait(false);
        attempt.ResultSummary = await BuildRetryFailureSummaryAsync(job, cancellationToken).ConfigureAwait(false);
        attempt.ActualSuccessPercent = 0;
        await SaveChangesWithRecoveryConcurrencyRetryAsync(cancellationToken).ConfigureAwait(false);

        await audit.RecordAdminActionAsync(
            SourceRecoveryAuditEvents.RetryFailed,
            "SourceRecoveryAttempt",
            attempt.Id.ToString(),
            new { job.Id, job.ErrorMessage },
            cancellationToken).ConfigureAwait(false);

        return ToApplyResult(attempt);
    }

    public async Task ReconcileUnfinalizedAttemptsAsync(CancellationToken cancellationToken)
    {
        var attemptIds = new HashSet<Guid>();

        var pending = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.Status == SourceRecoveryAttemptStatus.RetryEnqueued
                        && a.RetryDownloadJobId != null)
            .Select(a => new { a.Id, RetryJobId = a.RetryDownloadJobId!.Value })
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count > 0)
        {
            var retryJobIds = pending.Select(p => p.RetryJobId).ToList();
            var terminalJobs = await db.DownloadJobs.AsNoTracking()
                .Where(j => retryJobIds.Contains(j.Id)
                            && (j.Status == DownloadJobStatus.Succeeded
                                || j.Status == DownloadJobStatus.Failed
                                || j.Status == DownloadJobStatus.SuccessWithAutoAiRecovery
                                || j.Status == DownloadJobStatus.FailedAfterAutoAiRecovery
                                || j.Status == DownloadJobStatus.Cancelled
                                || j.Status == DownloadJobStatus.ManualInterventionRequired
                                || j.Status == DownloadJobStatus.AutoAiRecoverySkipped))
                .Select(j => j.Id)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in pending.Where(p => terminalJobs.Contains(p.RetryJobId)))
            {
                attemptIds.Add(row.Id);
            }
        }

        var orphanedJobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.CorrelationId.StartsWith("recovery:")
                        && (j.Status == DownloadJobStatus.Succeeded
                            || j.Status == DownloadJobStatus.Failed
                            || j.Status == DownloadJobStatus.SuccessWithAutoAiRecovery
                            || j.Status == DownloadJobStatus.FailedAfterAutoAiRecovery
                            || j.Status == DownloadJobStatus.Cancelled
                            || j.Status == DownloadJobStatus.ManualInterventionRequired
                            || j.Status == DownloadJobStatus.AutoAiRecoverySkipped))
            .Select(j => new { j.CorrelationId, j.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var job in orphanedJobs)
        {
            if (!Guid.TryParse(job.CorrelationId["recovery:".Length..], out var attemptId))
            {
                continue;
            }

            var needsHeal = await db.SourceRecoveryAttempts.AsNoTracking()
                .AnyAsync(
                    a => a.Id == attemptId
                         && !a.IsDeleted
                         && a.AppliedAt == null
                         && a.Status == SourceRecoveryAttemptStatus.AnalysisGenerated,
                    cancellationToken)
                .ConfigureAwait(false);
            if (needsHeal)
            {
                attemptIds.Add(attemptId);
            }
        }

        var retryEnqueuedWithoutAppliedAt = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.Status == SourceRecoveryAttemptStatus.RetryEnqueued
                        && a.AppliedAt == null)
            .Select(a => a.Id)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var attemptId in retryEnqueuedWithoutAppliedAt)
        {
            attemptIds.Add(attemptId);
        }

        foreach (var attemptId in attemptIds)
        {
            try
            {
                await FinalizeAttemptAsync(attemptId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not reconcile unfinalized recovery attempt {AttemptId}.", attemptId);
            }
        }
    }

    public async Task RollbackAsync(Guid attemptId, Guid actorUserId, string reason, CancellationToken cancellationToken)
    {
        var attempt = await db.SourceRecoveryAttempts
            .Include(a => a.NewsSource)
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recovery attempt not found.");

        await RollbackInternalAsync(attempt, reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task RollbackInternalAsync(SourceRecoveryAttempt attempt, string reason, CancellationToken cancellationToken)
    {
        if (attempt.RollbackVersionId is null)
        {
            return;
        }

        var rollback = await db.SourceConfigurationVersions
            .FirstOrDefaultAsync(v => v.Id == attempt.RollbackVersionId, cancellationToken)
            .ConfigureAwait(false);
        var source = attempt.NewsSource;
        SourceRecoveryConfigurationSnapshot? rollbackSnapshot = null;
        if (rollback is not null)
        {
            rollbackSnapshot = SourceRecoveryConfigurationSnapshot.FromJson(rollback.JsonConfiguration);
            if (rollbackSnapshot is not null)
            {
                rollbackSnapshot.ApplyPatch(rollbackSnapshot.ToPatch(), source);
            }

            source.ModifiedAt = DateTimeOffset.UtcNow;
            rollback.Status = SourceConfigurationVersionStatus.Active;
            if (attempt.CandidateVersionId is Guid candidateId)
            {
                var candidate = await db.SourceConfigurationVersions
                    .FirstOrDefaultAsync(v => v.Id == candidateId, cancellationToken)
                    .ConfigureAwait(false);
                if (candidate is not null)
                {
                    candidate.Status = SourceConfigurationVersionStatus.Archived;
                }
            }
        }

        attempt.Status = SourceRecoveryAttemptStatus.RolledBack;
        attempt.ResultSummary = reason;
        attempt.CompletedAt = DateTimeOffset.UtcNow;
        await SaveChangesWithRecoveryConcurrencyRetryAsync(
            cancellationToken,
            onNewsSourceConflict: rollbackSnapshot is not null
                ? sourceEntity => rollbackSnapshot.ApplyPatch(rollbackSnapshot.ToPatch(), sourceEntity)
                : null).ConfigureAwait(false);

        await audit.RecordAdminActionAsync(
            SourceRecoveryAuditEvents.RollbackExecuted,
            "SourceRecoveryAttempt",
            attempt.Id.ToString(),
            new { reason },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ActivateCandidateAsync(SourceRecoveryAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt.CandidateVersionId is null)
        {
            return;
        }

        var versions = await db.SourceConfigurationVersions
            .Where(v => !v.IsDeleted && v.NewsSourceId == attempt.NewsSourceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var v in versions.Where(v => v.Status == SourceConfigurationVersionStatus.Active))
        {
            v.Status = SourceConfigurationVersionStatus.Archived;
        }

        var candidate = versions.FirstOrDefault(v => v.Id == attempt.CandidateVersionId);
        if (candidate is not null)
        {
            candidate.Status = SourceConfigurationVersionStatus.Active;
        }
    }

    private async Task TryRecordKnowledgeAsync(SourceRecoveryAttempt attempt, bool success, CancellationToken cancellationToken)
    {
        try
        {
            await RecordKnowledgeAsync(attempt, success, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record recovery knowledge for attempt {AttemptId}.", attempt.Id);
        }
    }

    private static SourceRecoveryOptionDto? GetSelectedOption(SourceRecoveryAttempt attempt)
    {
        if (attempt.SelectedOptionIndex < 0)
        {
            return null;
        }

        var options = SourceRecoveryJsonParser.ParseOptions(attempt.AnalysisJson);
        return options.FirstOrDefault(o => o.OptionIndex == attempt.SelectedOptionIndex);
    }

    private async Task RecordKnowledgeAsync(SourceRecoveryAttempt attempt, bool success, CancellationToken cancellationToken)
    {
        var option = GetSelectedOption(attempt);
        if (option is null)
        {
            return;
        }

        var source = attempt.NewsSource;
        foreach (var field in option.AffectedFields)
        {
            var patch = option.Patch;
            var newValue = field switch
            {
                "UsernameSelector" => patch.UsernameSelector,
                "PasswordSelector" => patch.PasswordSelector,
                "SubmitSelector" => patch.SubmitSelector,
                "DownloadSelector" => patch.DownloadSelector,
                "LoginIconSelector" => patch.LoginIconSelector,
                "DownloadMenuItemSelector" => patch.DownloadMenuItemSelector,
                "PdfLinkSelector" => patch.PdfLinkSelector,
                "PdfDownloadSelector" => patch.PdfDownloadSelector,
                "BaseUrl" => patch.BaseUrl,
                "EditionUrl" => patch.EditionUrl,
                "PdfDiscoveryPageUrl" => patch.PdfDiscoveryPageUrl,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(newValue))
            {
                continue;
            }

            db.SourceRecoveryKnowledgeEntries.Add(new SourceRecoveryKnowledgeEntry
            {
                Id = Guid.NewGuid(),
                FailureType = attempt.FailureType,
                PortalStrategyKey = source.PortalStrategyKey,
                ConnectorKey = source.ConnectorKey,
                FieldName = field,
                NewSelector = newValue,
                SuccessCount = success ? 1 : 0,
                FailureCount = success ? 0 : 1,
                SourceRecoveryAttemptId = attempt.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(SourceRecoveryAttempt Attempt, SourceRecoveryOptionDto Option)> LoadAttemptOptionAsync(
        Guid attemptId,
        int optionIndex,
        CancellationToken cancellationToken)
    {
        DetachTrackedEntity<SourceRecoveryAttempt>(attemptId);

        var attempt = await db.SourceRecoveryAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recovery attempt not found.");

        var options = SourceRecoveryJsonParser.ParseOptions(attempt.AnalysisJson);
        var option = options.FirstOrDefault(o => o.OptionIndex == optionIndex)
                     ?? throw new InvalidOperationException("Recovery option not found.");
        return (attempt, option);
    }

    private async Task<NewsSource> ReloadMutableSourceAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        DetachTrackedEntity<NewsSource>(sourceId);
        return await db.NewsSources
            .FirstAsync(s => s.Id == sourceId, cancellationToken)
            .ConfigureAwait(false);
    }

    private void DetachTrackedEntity<TEntity>(Guid entityId)
        where TEntity : class
    {
        if (db is not DbContext context)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<TEntity>().Where(e => GetEntityId(e.Entity) == entityId).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static Guid GetEntityId(object entity) => entity switch
    {
        NewsSource source => source.Id,
        SourceRecoveryAttempt attempt => attempt.Id,
        _ => throw new InvalidOperationException($"Unsupported tracked entity type {entity.GetType().Name}.")
    };

    private static void EnsureAttemptCanBeApplied(SourceRecoveryAttempt attempt)
    {
        switch (attempt.Status)
        {
            case SourceRecoveryAttemptStatus.PendingAdminApproval:
                throw new InvalidOperationException("This fix is awaiting admin approval.");
            case SourceRecoveryAttemptStatus.RetryEnqueued:
            case SourceRecoveryAttemptStatus.CandidateApplied:
            case SourceRecoveryAttemptStatus.Succeeded:
                throw new InvalidOperationException(
                    "This recovery suggestion was already applied. Run Analyze with AI again after a new failure.");
        }
    }

    private static void ApplyAttemptMutations(
        SourceRecoveryAttempt attempt,
        int optionIndex,
        Guid actorUserId,
        SourceRecoveryOptionDto option,
        Guid candidateVersionId,
        Guid rollbackVersionId,
        Guid retryJobId,
        SourceRecoveryAttemptStatus status)
    {
        attempt.SelectedOptionIndex = optionIndex;
        attempt.CandidateVersionId = candidateVersionId;
        attempt.RollbackVersionId = rollbackVersionId;
        attempt.AppliedByUserId = actorUserId;
        attempt.AppliedAt = DateTimeOffset.UtcNow;
        attempt.PredictedSuccessPercent = option.PredictedSuccessPercent;
        attempt.RetryDownloadJobId = retryJobId;
        attempt.Status = status;
    }

    private Task SaveChangesWithApplyConcurrencyRetryAsync(
        ApplyAttemptPersistence persistence,
        SourceRecoveryConfigurationPatchDto patch,
        CancellationToken cancellationToken) =>
        SaveChangesWithRecoveryConcurrencyRetryAsync(
            cancellationToken,
            onNewsSourceConflict: source =>
            {
                SourceRecoveryConfigurationSnapshot.FromEntity(source).ApplyPatch(patch, source);
                source.ModifiedAt = DateTimeOffset.UtcNow;
            },
            onSourceRecoveryAttemptConflict: recoveryAttempt =>
            {
                ApplyAttemptMutations(
                    recoveryAttempt,
                    persistence.OptionIndex,
                    persistence.ActorUserId,
                    persistence.Option,
                    persistence.CandidateVersionId,
                    persistence.RollbackVersionId,
                    persistence.RetryJobId,
                    SourceRecoveryAttemptStatus.RetryEnqueued);
            });

    private Task SaveChangesWithSourceConcurrencyRetryAsync(
        SourceRecoveryConfigurationPatchDto patch,
        CancellationToken cancellationToken) =>
        SaveChangesWithRecoveryConcurrencyRetryAsync(
            cancellationToken,
            onNewsSourceConflict: source =>
            {
                SourceRecoveryConfigurationSnapshot.FromEntity(source).ApplyPatch(patch, source);
                source.ModifiedAt = DateTimeOffset.UtcNow;
            });

    private async Task<bool> TryHealOrphanedApplyAsync(
        SourceRecoveryAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (attempt.AppliedAt is not null)
        {
            return false;
        }

        var orphanJob = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.NewsSourceId == attempt.NewsSourceId
                        && j.CorrelationId == $"recovery:{attempt.Id:N}")
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (orphanJob is null)
        {
            return false;
        }

        var versions = await db.SourceConfigurationVersions
            .Where(v => !v.IsDeleted && v.SourceRecoveryAttemptId == attempt.Id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        attempt.RetryDownloadJobId = orphanJob.Id;
        attempt.AppliedAt = orphanJob.CreatedAt;
        attempt.Status = SourceRecoveryAttemptStatus.RetryEnqueued;
        attempt.RollbackVersionId ??= versions
            .FirstOrDefault(v => v.Status == SourceConfigurationVersionStatus.Rollback)?.Id;
        attempt.CandidateVersionId ??= versions
            .FirstOrDefault(v => v.Status == SourceConfigurationVersionStatus.Candidate)?.Id;

        await SaveChangesWithRecoveryConcurrencyRetryAsync(cancellationToken).ConfigureAwait(false);

        logger.LogWarning(
            "Repaired split recovery-apply state for attempt {AttemptId} using retry job {JobId}.",
            attempt.Id,
            orphanJob.Id);

        return true;
    }

    private async Task SaveChangesWithRecoveryConcurrencyRetryAsync(
        CancellationToken cancellationToken,
        Action<NewsSource>? onNewsSourceConflict = null,
        Action<SourceRecoveryAttempt>? onSourceRecoveryAttemptConflict = null)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < 2)
            {
                logger.LogWarning(
                    ex,
                    "Concurrency conflict while persisting AI source recovery; refreshing tracked rows and retrying.");

                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is NewsSource newsSource)
                    {
                        var dbValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
                        if (dbValues is null)
                        {
                            throw new InvalidOperationException("News source was removed before recovery could be finalized.");
                        }

                        entry.OriginalValues.SetValues(dbValues);
                        entry.CurrentValues.SetValues(dbValues);
                        onNewsSourceConflict?.Invoke(newsSource);
                        newsSource.ModifiedAt = DateTimeOffset.UtcNow;
                    }
                    else if (entry.Entity is SourceRecoveryAttempt recoveryAttempt)
                    {
                        await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                        onSourceRecoveryAttemptConflict?.Invoke(recoveryAttempt);
                    }
                    else
                    {
                        await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private sealed record ApplyAttemptPersistence(
        int OptionIndex,
        Guid ActorUserId,
        SourceRecoveryOptionDto Option,
        Guid CandidateVersionId,
        Guid RollbackVersionId,
        Guid RetryJobId);

    private static bool IsRetryStillInProgress(DownloadJobStatus status) =>
        status is DownloadJobStatus.Running
            or DownloadJobStatus.Pending
            or DownloadJobStatus.AutoAiRecoveryAnalyzing
            or DownloadJobStatus.AutoAiRecoveryApplying
            or DownloadJobStatus.AutoAiRecoveryRetrying;

    private static bool IsRetrySuccessful(DownloadJobStatus status) =>
        status is DownloadJobStatus.Succeeded or DownloadJobStatus.SuccessWithAutoAiRecovery;

    private static bool IsRetryTerminal(DownloadJobStatus status) =>
        status is DownloadJobStatus.Succeeded
            or DownloadJobStatus.Failed
            or DownloadJobStatus.SuccessWithAutoAiRecovery
            or DownloadJobStatus.FailedAfterAutoAiRecovery
            or DownloadJobStatus.Cancelled
            or DownloadJobStatus.ManualInterventionRequired
            or DownloadJobStatus.AutoAiRecoverySkipped;

    private static SourceRecoveryApplyResultDto ToApplyResult(SourceRecoveryAttempt attempt) =>
        new(
            attempt.Id,
            attempt.CandidateVersionId ?? Guid.Empty,
            attempt.RollbackVersionId,
            attempt.Status,
            attempt.ResultSummary ?? "Recovery attempt completed.");

    private async Task<int> GetNextVersionNumberAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        var max = await db.SourceConfigurationVersions.AsNoTracking()
            .Where(v => !v.IsDeleted && v.NewsSourceId == sourceId)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken)
            .ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    private async Task<string> BuildRetryFailureSummaryAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        var audit = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == job.Id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (audit is not null
            && !string.IsNullOrWhiteSpace(audit.Message)
            && (string.IsNullOrWhiteSpace(job.ErrorMessage)
                || job.ErrorMessage.Contains("No files were downloaded", StringComparison.OrdinalIgnoreCase)))
        {
            return string.IsNullOrWhiteSpace(audit.FailureCode)
                ? audit.Message
                : $"{audit.FailureCode}: {audit.Message}";
        }

        return job.ErrorMessage ?? "Download retry failed.";
    }

    private static IReadOnlyList<(string Field, string? CurrentValue, string? NewValue)> BuildChangeList(
        SourceRecoveryConfigurationPatchDto current,
        SourceRecoveryConfigurationPatchDto patch)
    {
        var list = new List<(string, string?, string?)>();
        void Add(string name, string? cur, string? next)
        {
            if (next is not null && !string.Equals(cur, next, StringComparison.Ordinal))
            {
                list.Add((name, cur, next));
            }
        }

        Add("UsernameSelector", current.UsernameSelector, patch.UsernameSelector);
        Add("PasswordSelector", current.PasswordSelector, patch.PasswordSelector);
        Add("SubmitSelector", current.SubmitSelector, patch.SubmitSelector);
        Add("DownloadSelector", current.DownloadSelector, patch.DownloadSelector);
        Add("LoginIconSelector", current.LoginIconSelector, patch.LoginIconSelector);
        Add("NewspaperCanvasSelector", current.NewspaperCanvasSelector, patch.NewspaperCanvasSelector);
        Add("ContextMenuSelector", current.ContextMenuSelector, patch.ContextMenuSelector);
        Add("DownloadMenuItemSelector", current.DownloadMenuItemSelector, patch.DownloadMenuItemSelector);
        Add("LoginSuccessSelector", current.LoginSuccessSelector, patch.LoginSuccessSelector);
        Add("SuccessUrlPattern", current.SuccessUrlPattern, patch.SuccessUrlPattern);
        Add("PdfDownloadSelector", current.PdfDownloadSelector, patch.PdfDownloadSelector);
        Add("PdfLinkSelector", current.PdfLinkSelector, patch.PdfLinkSelector);
        Add("BaseUrl", current.BaseUrl, patch.BaseUrl);
        Add("EditionUrl", current.EditionUrl, patch.EditionUrl);
        Add("PdfDiscoveryPageUrl", current.PdfDiscoveryPageUrl, patch.PdfDiscoveryPageUrl);
        if (patch.DownloadWaitTimeoutSeconds is int wait && wait != current.DownloadWaitTimeoutSeconds)
        {
            list.Add(("DownloadWaitTimeoutSeconds", current.DownloadWaitTimeoutSeconds.ToString(), wait.ToString()));
        }

        if (patch.UseHeadlessBrowser is bool headless && headless != current.UseHeadlessBrowser)
        {
            list.Add(("UseHeadlessBrowser", current.UseHeadlessBrowser.ToString(), headless.ToString()));
        }

        return list;
    }

    private static bool IsRecoveryEligibleSource(NewsSource source) =>
        source.SourceType == NewsSourceType.WebPortalLogin
        || (source.PdfDiscoveryEnabled
            && source.SourceType is NewsSourceType.PublicPdf or NewsSourceType.PublicHtml);
}
