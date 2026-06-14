using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.PdfEdition;
using MIP.Aws.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class OperatorDownloadMonitorService(
    IApplicationDbContext db,
    IReportEmailSender emailSender,
    IPdfEditionFailureArtifactService failureArtifacts,
    PortalArtifactUrlBuilder artifactUrls,
    ISourceRecoveryOrchestrator recoveryOrchestrator,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    IOptions<MailAutomationOptions> mailAutomation,
    ILogger<OperatorDownloadMonitorService> logger) : IOperatorDownloadMonitorService
{
    private static readonly TimeSpan StaleRunningJobThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RecoveryRunningJobThreshold = TimeSpan.FromMinutes(5);

    public async Task<DownloadMonitorDto> GetMonitorAsync(DateOnly? monitorDate, CancellationToken cancellationToken)
    {
        await recoveryOrchestrator.ReconcileUnfinalizedAttemptsAsync(cancellationToken).ConfigureAwait(false);
        await ReconcileStaleDownloadJobsAsync(cancellationToken).ConfigureAwait(false);

        var date = monitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (dayStart, dayEnd) = DayBounds(date);
        var sources = (await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .ToList();

        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.CreatedAt >= dayStart && j.CreatedAt < dayEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pdfRows = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted && (p.EditionDate == date
                                         || (p.DownloadedAt >= dayStart && p.DownloadedAt < dayEnd)
                                         || (p.DiscoveredAt >= dayStart && p.DiscoveredAt < dayEnd)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var interventionJobIds = await LoadActiveInterventionJobIdsAsync(cancellationToken).ConfigureAwait(false);

        var fileIdByJobId = await LoadLatestFileIdBySucceededJobIdAsync(jobs, cancellationToken).ConfigureAwait(false);
        var recoveryRetryJobAttemptIds = await LoadRecoveryRetryJobAttemptIdsAsync(jobs, cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<DownloadMonitorSourceRowDto>();
        var attention = new List<AttentionSourceDto>();
        var successCount = 0;
        var failedCount = 0;
        var manualCount = 0;
        var pdfCount = 0;

        foreach (var source in sources)
        {
            var row = BuildSourceRow(source, date, dayStart, dayEnd, jobs, pdfRows, interventionJobIds, fileIdByJobId, recoveryRetryJobAttemptIds);
            rows.Add(row);

            if (DownloadMonitorStatusLabels.IsSuccessful(row.LastDownloadStatus))
            {
                successCount++;
            }
            else if (row.LastDownloadStatus == DownloadMonitorStatusLabels.Failed)
            {
                failedCount++;
            }

            if (row.ManualInterventionRequired)
            {
                manualCount++;
                attention.Add(new AttentionSourceDto(
                    source.Id,
                    source.Name,
                    row.FailureReason ?? row.LastDownloadStatus,
                    row.SuggestedIntervention ?? "Inform Admin for manual review."));
            }

            if (row.LatestPdfFileId is not null && DownloadMonitorStatusLabels.IsSuccessful(row.LastDownloadStatus))
            {
                pdfCount++;
            }
        }

        var pendingNotifications = CountRelevantPendingAdminAlerts(rows, interventionJobIds.PendingJobIds);

        var summary = new DownloadMonitorSummaryDto(
            sources.Count,
            successCount,
            failedCount,
            manualCount,
            pdfCount,
            pendingNotifications,
            attention.OrderBy(a => a.SourceName).ToList());

        return new DownloadMonitorDto(date, summary, rows);
    }

    public async Task<SourceDownloadStatusDto?> GetSourceStatusAsync(
        Guid sourceId,
        DateOnly? monitorDate,
        CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (source is null || !PdfManagementSourceRules.IsPdfDownloadMonitoredSource(source))
        {
            return null;
        }

        var date = monitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (dayStart, dayEnd) = DayBounds(date);
        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.NewsSourceId == sourceId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pdfRows = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted && p.NewsSourceId == sourceId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var interventionJobIds = await LoadActiveInterventionJobIdsAsync(cancellationToken).ConfigureAwait(false);

        var fileIdByJobId = await LoadLatestFileIdBySucceededJobIdAsync(jobs, cancellationToken).ConfigureAwait(false);
        var recoveryRetryJobAttemptIds = await LoadRecoveryRetryJobAttemptIdsAsync(jobs, cancellationToken)
            .ConfigureAwait(false);
        var row = BuildSourceRow(source, date, dayStart, dayEnd, jobs, pdfRows, interventionJobIds, fileIdByJobId, recoveryRetryJobAttemptIds);
        var timeline = jobs.Select(j => new DownloadAttemptTimelineDto(
                j.Id,
                MapJobStatus(j.Status),
                j.CompletedAt ?? j.StartedAt ?? j.CreatedAt,
                j.ErrorMessage,
                ExtractFailureCode(j.ErrorMessage)))
            .Concat(pdfRows.Select(p => new DownloadAttemptTimelineDto(
                p.DownloadJobId,
                MapPdfStatus(p.Status),
                p.DownloadedAt ?? p.DiscoveredAt ?? p.CreatedAt,
                p.FailureReason,
                MapPdfFailureCode(p.Status))))
            .OrderByDescending(t => t.AttemptedAt)
            .Take(15)
            .ToList();

        return new SourceDownloadStatusDto(
            source.Id,
            source.Name,
            row.LastDownloadStatus,
            row.LastDownloadTime,
            row.LastSuccessfulDownload,
            row.LastFailedDownload,
            row.FailureReason,
            row.FailureCode,
            row.ManualInterventionRequired,
            row.AdminInformed,
            row.SuggestedIntervention,
            timeline);
    }

    public async Task<LatestPdfLinkDto?> GetLatestPdfLinkAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return null;
        }

        var file = await db.DownloadedFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && f.DownloadJob!.NewsSourceId == sourceId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.Id, f.DownloadJobId, f.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (file is null)
        {
            var pdfEdition = await db.PdfEditionDownloads.AsNoTracking()
                .Where(p => !p.IsDeleted && p.NewsSourceId == sourceId && p.Status == PdfEditionStatus.Downloaded
                            && p.DownloadedFileId != null)
                .OrderByDescending(p => p.DownloadedAt)
                .Select(p => new { FileId = p.DownloadedFileId!.Value, p.DownloadJobId, p.DownloadedAt })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pdfEdition is null)
            {
                return new LatestPdfLinkDto(sourceId, null, null, null, null, false, null);
            }

            return BuildPdfLink(sourceId, pdfEdition.FileId, pdfEdition.DownloadJobId, pdfEdition.DownloadedAt);
        }

        return BuildPdfLink(sourceId, file.Id, file.DownloadJobId, file.CreatedAt);
    }

    public async Task<DownloadFailureDetailsDto?> GetFailureDetailsAsync(Guid downloadJobId, CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .Include(j => j.NewsSource)
            .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        var source = job.NewsSource;
        var auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == downloadJobId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var (screenshotPath, htmlSnapshotPath) = PortalAuditArtifactResolver.ResolvePaths(auditLogs);
        if (job.Status == DownloadJobStatus.Failed)
        {
            var captureUrl = await ResolveFailureCaptureUrlAsync(downloadJobId, source, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(captureUrl)
                && !await artifactUrls.HasUsableScreenshotAsync(auditLogs, cancellationToken).ConfigureAwait(false))
            {
                await failureArtifacts.EnsureFailureArtifactsAsync(
                    source.Id,
                    downloadJobId,
                    captureUrl,
                    job.ErrorMessage ?? "Download failed.",
                    ExtractFailureCode(job.ErrorMessage),
                    cancellationToken).ConfigureAwait(false);

                auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
                    .Where(a => !a.IsDeleted && a.DownloadJobId == downloadJobId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var (screenshotUrl, htmlSnapshotUrl) = await artifactUrls.BuildUrlsAsync(auditLogs, cancellationToken)
            .ConfigureAwait(false);

        var audit = auditLogs.FirstOrDefault();
        var failureCode = audit?.FailureCode ?? ExtractFailureCode(job.ErrorMessage);
        var complianceBlocked = !source.IsDownloadAllowed
            || failureCode is "DownloadNotAllowed" or "ComplianceBlocked";
        var suggested = ManualInterventionSuggestions.GetSuggestion(
            failureCode,
            job.ErrorMessage,
            source.RequiresManualAction,
            complianceBlocked);

        var notes = await db.DownloadOperatorNotes.AsNoTracking()
            .Where(n => !n.IsDeleted && n.DownloadJobId == downloadJobId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new DownloadOperatorNoteDto(n.Id, n.Note, n.CreatedByUserId, n.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastSuccess = await GetLatestPdfLinkAsync(source.Id, cancellationToken).ConfigureAwait(false);

        return new DownloadFailureDetailsDto(
            source.Id,
            source.Name,
            job.Id,
            failureCode,
            job.ErrorMessage ?? "Download failed without a detailed message.",
            source.EditionUrl ?? source.BaseUrl,
            job.CompletedAt ?? job.StartedAt ?? job.CreatedAt,
            screenshotUrl,
            htmlSnapshotUrl,
            job.RetryCount,
            suggested,
            complianceBlocked,
            source.RequiresManualAction,
            lastSuccess?.Available == true ? lastSuccess : null,
            notes);
    }

    public async Task<AiRecoverySuccessDetailsDto?> GetAiRecoverySuccessDetailsAsync(
        Guid recoveryDownloadJobId,
        CancellationToken cancellationToken)
    {
        var recoveryJob = await db.DownloadJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == recoveryDownloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (recoveryJob is null || recoveryJob.Status != DownloadJobStatus.Succeeded)
        {
            return null;
        }

        var attempt = await FindRecoveryAttemptForJobAsync(recoveryDownloadJobId, recoveryJob, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null)
        {
            return null;
        }

        attempt = await EnsureRecoveryAttemptFinalizedAsync(attempt, recoveryDownloadJobId, cancellationToken)
            .ConfigureAwait(false);

        return await BuildAiRecoverySuccessDetailsAsync(attempt, recoveryJob, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiRecoverySuccessDetailsDto?> GetAiRecoverySuccessDetailsByAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await db.SourceRecoveryAttempts.AsNoTracking()
            .Include(a => a.NewsSource)
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null || attempt.Status != SourceRecoveryAttemptStatus.Succeeded)
        {
            return null;
        }

        DownloadJob? recoveryJob = null;
        if (attempt.RetryDownloadJobId is Guid retryJobId)
        {
            recoveryJob = await db.DownloadJobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == retryJobId && !j.IsDeleted, cancellationToken)
                .ConfigureAwait(false);
            attempt = await EnsureRecoveryAttemptFinalizedAsync(attempt, retryJobId, cancellationToken)
                .ConfigureAwait(false);
        }

        return await BuildAiRecoverySuccessDetailsAsync(attempt, recoveryJob, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SourceRecoveryAttempt> EnsureRecoveryAttemptFinalizedAsync(
        SourceRecoveryAttempt attempt,
        Guid recoveryDownloadJobId,
        CancellationToken cancellationToken)
    {
        if (attempt.Status is not (SourceRecoveryAttemptStatus.RetryEnqueued
            or SourceRecoveryAttemptStatus.CandidateApplied))
        {
            return attempt;
        }

        try
        {
            await recoveryOrchestrator.FinalizeAttemptAsync(attempt.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not finalize recovery attempt {AttemptId} while loading AI recovery details.",
                attempt.Id);
        }

        var recoveryJob = await db.DownloadJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == recoveryDownloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (recoveryJob is null)
        {
            return attempt;
        }

        return await FindRecoveryAttemptForJobAsync(recoveryDownloadJobId, recoveryJob, cancellationToken)
            .ConfigureAwait(false)
            ?? attempt;
    }

    private async Task<AiRecoverySuccessDetailsDto?> BuildAiRecoverySuccessDetailsAsync(
        SourceRecoveryAttempt attempt,
        DownloadJob? recoveryJob,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SourceRecoveryOptionDto> options;
        try
        {
            options = string.IsNullOrWhiteSpace(attempt.AnalysisJson)
                ? Array.Empty<SourceRecoveryOptionDto>()
                : SourceRecoveryJsonParser.ParseOptions(attempt.AnalysisJson);
        }
        catch
        {
            options = Array.Empty<SourceRecoveryOptionDto>();
        }

        var versions = await db.SourceConfigurationVersions.AsNoTracking()
            .Where(v => !v.IsDeleted && v.SourceRecoveryAttemptId == attempt.Id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recoveryJobCreatedAt = recoveryJob?.CreatedAt ?? attempt.AppliedAt ?? attempt.CreatedAt;
        var selected = ResolveAppliedRecoveryOption(attempt, options, versions, recoveryJobCreatedAt);
        var rollbackJson = versions
            .FirstOrDefault(v => v.Id == attempt.RollbackVersionId)
            ?? versions.FirstOrDefault(v => v.Status == SourceConfigurationVersionStatus.Rollback);
        var candidateJson = versions
            .FirstOrDefault(v => v.Id == attempt.CandidateVersionId)
            ?? versions.FirstOrDefault(v => v.Status == SourceConfigurationVersionStatus.Candidate
                                            || v.Status == SourceConfigurationVersionStatus.Active);

        var before = SourceRecoveryConfigurationSnapshot.FromJson(rollbackJson?.JsonConfiguration)?.ToPatch()
                     ?? new SourceRecoveryConfigurationPatchDto(
                         null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var after = SourceRecoveryConfigurationSnapshot.FromJson(candidateJson?.JsonConfiguration)?.ToPatch()
                    ?? selected?.Patch
                    ?? before;

        var changes = SourceRecoveryChangeListBuilder.Build(before, after)
            .Select(c => new AiRecoveryConfigurationChangeDto(c.Field, c.BeforeValue, c.AfterValue))
            .ToList();

        DateTimeOffset? originalFailedAt = null;
        string? screenshotUrl = null;
        string? htmlSnapshotUrl = null;
        if (attempt.DownloadJobId is Guid originalJobId)
        {
            var originalJob = await db.DownloadJobs.AsNoTracking()
                .Include(j => j.NewsSource)
                .FirstOrDefaultAsync(j => j.Id == originalJobId && !j.IsDeleted, cancellationToken)
                .ConfigureAwait(false);
            if (originalJob is not null)
            {
                originalFailedAt = originalJob.CompletedAt ?? originalJob.StartedAt ?? originalJob.CreatedAt;
                var auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
                    .Where(a => !a.IsDeleted && a.DownloadJobId == originalJobId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (originalJob.Status == DownloadJobStatus.Failed)
                {
                    var captureUrl = await ResolveFailureCaptureUrlAsync(
                            originalJobId,
                            originalJob.NewsSource,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(captureUrl)
                        && !await artifactUrls.HasUsableScreenshotAsync(auditLogs, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        await failureArtifacts.EnsureFailureArtifactsAsync(
                            originalJob.NewsSourceId,
                            originalJobId,
                            captureUrl,
                            originalJob.ErrorMessage ?? attempt.FailureMessage,
                            attempt.FailureCode ?? ExtractFailureCode(originalJob.ErrorMessage),
                            cancellationToken).ConfigureAwait(false);

                        auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
                            .Where(a => !a.IsDeleted && a.DownloadJobId == originalJobId)
                            .OrderByDescending(a => a.CreatedAt)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                (screenshotUrl, htmlSnapshotUrl) = await artifactUrls.BuildUrlsAsync(auditLogs, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var appliedBy = attempt.IsAutomatic
            ? "Automatic AI Recovery"
            : "—";
        if (!attempt.IsAutomatic && attempt.AppliedByUserId is Guid userId)
        {
            var user = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Email, u.UserName })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            appliedBy = user?.Email ?? user?.UserName ?? userId.ToString();
        }

        var outcomeSummary = attempt.ResultSummary;
        if (string.IsNullOrWhiteSpace(outcomeSummary))
        {
            outcomeSummary = attempt.Status == SourceRecoveryAttemptStatus.Succeeded
                ? "Download retry succeeded; candidate configuration activated."
                : "Download retry succeeded after AI recovery.";
        }

        return new AiRecoverySuccessDetailsDto(
            attempt.Id,
            attempt.NewsSourceId,
            attempt.NewsSource.Name,
            attempt.DownloadJobId,
            recoveryJob?.Id ?? attempt.RetryDownloadJobId,
            attempt.FailureType,
            attempt.FailureCode,
            attempt.FailureMessage,
            originalFailedAt,
            screenshotUrl,
            htmlSnapshotUrl,
            selected?.Title ?? candidateJson?.Reason?.Replace("AI recovery: ", string.Empty) ?? "AI recovery option",
            selected?.Description ?? string.Empty,
            selected?.ExpectedFix,
            attempt.PredictedSuccessPercent ?? selected?.PredictedSuccessPercent,
            attempt.ActualSuccessPercent ?? (recoveryJob?.Status == DownloadJobStatus.Succeeded ? 100 : null),
            appliedBy,
            attempt.AppliedAt,
            attempt.CompletedAt ?? recoveryJob?.CompletedAt,
            outcomeSummary,
            changes,
            attempt.IsAutomatic);
    }

    private async Task<SourceRecoveryAttempt?> FindRecoveryAttemptForJobAsync(
        Guid recoveryDownloadJobId,
        DownloadJob recoveryJob,
        CancellationToken cancellationToken)
    {
        var attempt = await db.SourceRecoveryAttempts.AsNoTracking()
            .Include(a => a.NewsSource)
            .FirstOrDefaultAsync(
                a => !a.IsDeleted && a.RetryDownloadJobId == recoveryDownloadJobId,
                cancellationToken)
            .ConfigureAwait(false);

        if (attempt is not null)
        {
            return attempt;
        }

        if (string.IsNullOrWhiteSpace(recoveryJob.CorrelationId)
            || !recoveryJob.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(recoveryJob.CorrelationId["recovery:".Length..], out var attemptId))
        {
            return null;
        }

        return await db.SourceRecoveryAttempts.AsNoTracking()
            .Include(a => a.NewsSource)
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    private static SourceRecoveryOptionDto? ResolveAppliedRecoveryOption(
        SourceRecoveryAttempt attempt,
        IReadOnlyList<SourceRecoveryOptionDto> options,
        IReadOnlyList<SourceConfigurationVersion> versions,
        DateTimeOffset recoveryJobCreatedAt)
    {
        if (attempt.SelectedOptionIndex >= 0)
        {
            return options.FirstOrDefault(o => o.OptionIndex == attempt.SelectedOptionIndex);
        }

        var candidateVersion = versions.FirstOrDefault(v => v.Id == attempt.CandidateVersionId)
            ?? versions
                .Where(v => v.Status is SourceConfigurationVersionStatus.Candidate
                    or SourceConfigurationVersionStatus.Active)
                .Where(v => v.CreatedAt <= recoveryJobCreatedAt.AddMinutes(1))
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

        if (candidateVersion?.Reason is string reason
            && reason.StartsWith("AI recovery: ", StringComparison.OrdinalIgnoreCase))
        {
            var title = reason["AI recovery: ".Length..];
            var byTitle = options.FirstOrDefault(o =>
                string.Equals(o.Title, title, StringComparison.OrdinalIgnoreCase));
            if (byTitle is not null)
            {
                return byTitle;
            }
        }

        return options.OrderByDescending(o => o.ConfidenceScore).FirstOrDefault();
    }

    public async Task<Guid> AddNoteAsync(Guid downloadJobId, string note, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException("Note text is required.");
        }

        var job = await db.DownloadJobs
            .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Download job was not found.");

        var entity = new DownloadOperatorNote
        {
            Id = Guid.NewGuid(),
            NewsSourceId = job.NewsSourceId,
            DownloadJobId = job.Id,
            Note = note.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.DownloadOperatorNotes.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<Guid> InformAdminAsync(
        Guid downloadJobId,
        string? operatorNote,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .Include(j => j.NewsSource)
            .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Download job was not found.");

        var details = await GetFailureDetailsAsync(downloadJobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Could not resolve failure details for this job.");

        var notification = new AdminInterventionNotification
        {
            Id = Guid.NewGuid(),
            NewsSourceId = job.NewsSourceId,
            DownloadJobId = job.Id,
            FailureReason = details.FailureMessage,
            FailureCode = details.FailureCode,
            SuggestedAction = details.SuggestedIntervention,
            OperatorNote = operatorNote?.Trim(),
            Status = AdminInterventionNotificationStatus.Pending,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.AdminInterventionNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TrySendAdminEmailAsync(job.NewsSource, notification, cancellationToken).ConfigureAwait(false);
        return notification.Id;
    }

    public async Task<IReadOnlyList<AdminInterventionNotificationDto>> GetInterventionNotificationsAsync(
        bool pendingOnly,
        CancellationToken cancellationToken)
    {
        var query = db.AdminInterventionNotifications.AsNoTracking()
            .Include(n => n.NewsSource)
            .Where(n => !n.IsDeleted);

        if (pendingOnly)
        {
            query = query.Where(n => n.Status == AdminInterventionNotificationStatus.Pending
                                     || n.Status == AdminInterventionNotificationStatus.Acknowledged);
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(n => new AdminInterventionNotificationDto(
            n.Id,
            n.NewsSourceId,
            n.NewsSource.Name,
            n.DownloadJobId,
            n.FailureReason,
            n.FailureCode,
            n.SuggestedAction,
            n.OperatorNote,
            n.Status.ToString(),
            n.CreatedAt,
            n.CreatedByUserId,
            n.AcknowledgedAt,
            n.ResolvedAt)).ToList();
    }

    public async Task AcknowledgeInterventionAsync(Guid notificationId, Guid adminUserId, CancellationToken cancellationToken)
    {
        var entity = await db.AdminInterventionNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Notification was not found.");

        entity.Status = AdminInterventionNotificationStatus.Acknowledged;
        entity.AcknowledgedByAdminId = adminUserId;
        entity.AcknowledgedAt = DateTimeOffset.UtcNow;
        entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResolveInterventionAsync(Guid notificationId, Guid adminUserId, CancellationToken cancellationToken)
    {
        var entity = await db.AdminInterventionNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Notification was not found.");

        entity.Status = AdminInterventionNotificationStatus.Resolved;
        entity.ResolvedByAdminId = adminUserId;
        entity.ResolvedAt = DateTimeOffset.UtcNow;
        entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pending admin alerts that still apply to today's current intervention job per source (excludes stale notifications).
    /// </summary>
    private static int CountRelevantPendingAdminAlerts(
        IReadOnlyList<DownloadMonitorSourceRowDto> rows,
        IReadOnlySet<Guid> pendingInterventionJobIds)
    {
        var currentInterventionJobIds = rows
            .Where(r => r.ManualInterventionRequired && r.LatestDownloadJobId is not null)
            .Select(r => r.LatestDownloadJobId!.Value)
            .ToHashSet();

        return pendingInterventionJobIds.Count(id => currentInterventionJobIds.Contains(id));
    }

    private async Task<(HashSet<Guid> PendingJobIds, HashSet<Guid> AcknowledgedJobIds)> LoadActiveInterventionJobIdsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await db.AdminInterventionNotifications.AsNoTracking()
            .Where(n => !n.IsDeleted
                        && n.DownloadJobId != null
                        && (n.Status == AdminInterventionNotificationStatus.Pending
                            || n.Status == AdminInterventionNotificationStatus.Acknowledged))
            .Select(n => new { JobId = n.DownloadJobId!.Value, n.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (
            rows.Where(r => r.Status == AdminInterventionNotificationStatus.Pending)
                .Select(r => r.JobId)
                .ToHashSet(),
            rows.Where(r => r.Status == AdminInterventionNotificationStatus.Acknowledged)
                .Select(r => r.JobId)
                .ToHashSet());
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> LoadRecoveryRetryJobAttemptIdsAsync(
        IReadOnlyList<DownloadJob> jobs,
        CancellationToken cancellationToken)
    {
        var jobIds = jobs.Select(j => j.Id).ToList();
        if (jobIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var rows = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.RetryDownloadJobId != null && jobIds.Contains(a.RetryDownloadJobId.Value))
            .Select(a => new { JobId = a.RetryDownloadJobId!.Value, a.Id, a.CompletedAt, a.AppliedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(r => r.JobId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CompletedAt ?? x.AppliedAt).First().Id);
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> LoadLatestFileIdBySucceededJobIdAsync(
        IReadOnlyList<DownloadJob> jobs,
        CancellationToken cancellationToken)
    {
        var succeededJobIds = jobs
            .Where(j => j.Status is DownloadJobStatus.Succeeded or DownloadJobStatus.SuccessWithAutoAiRecovery)
            .Select(j => j.Id)
            .ToList();

        if (succeededJobIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var files = await db.DownloadedFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && succeededJobIds.Contains(f.DownloadJobId))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.DownloadJobId, f.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return files
            .GroupBy(f => f.DownloadJobId)
            .ToDictionary(g => g.Key, g => g.First().Id);
    }

    private static DownloadMonitorSourceRowDto BuildSourceRow(
        NewsSource source,
        DateOnly date,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        IReadOnlyList<DownloadJob> allDayJobs,
        IReadOnlyList<PdfEditionDownload> allDayPdfs,
        (HashSet<Guid> PendingInterventionJobIds, HashSet<Guid> AcknowledgedInterventionJobIds) interventionJobIds,
        IReadOnlyDictionary<Guid, Guid> fileIdByJobId,
        IReadOnlyDictionary<Guid, Guid> recoveryRetryJobAttemptIds)
    {
        var sourceJobs = allDayJobs.Where(j => j.NewsSourceId == source.Id).OrderByDescending(j => j.CreatedAt).ToList();
        var sourcePdfs = allDayPdfs.Where(p => p.NewsSourceId == source.Id).OrderByDescending(p => p.CreatedAt).ToList();

        var latestJob = PickDisplayJob(sourceJobs, recoveryRetryJobAttemptIds);
        var latestPdf = sourcePdfs.FirstOrDefault();

        var lastActivity = PickLatestActivity(latestJob, latestPdf);
        var status = ResolveStatus(source, lastActivity.Job, lastActivity.Pdf);
        var failureReason = lastActivity.Job?.ErrorMessage ?? lastActivity.Pdf?.FailureReason;
        var failureCode = lastActivity.Job is not null
            ? ExtractFailureCode(lastActivity.Job.ErrorMessage)
            : lastActivity.Pdf is not null ? MapPdfFailureCode(lastActivity.Pdf.Status) : null;

        var complianceBlocked = status == DownloadMonitorStatusLabels.ComplianceBlocked;
        var manualRequired = ManualInterventionSuggestions.RequiresManualIntervention(
            status, failureCode, source.RequiresManualAction, complianceBlocked);

        DateTimeOffset? lastSuccess = LatestTerminalTimestamp(
            sourceJobs
                .Where(j => j.Status is DownloadJobStatus.Succeeded or DownloadJobStatus.SuccessWithAutoAiRecovery)
                .Select(j => j.CompletedAt ?? j.CreatedAt)
                .Concat(sourcePdfs.Where(p => p.Status == PdfEditionStatus.Downloaded).Select(p => p.DownloadedAt ?? p.CreatedAt)),
            dayStart,
            dayEnd);

        if (lastSuccess is null && source.LastPdfDownloadedAt is { } pdfAt
            && pdfAt >= dayStart && pdfAt < dayEnd)
        {
            lastSuccess = pdfAt;
        }

        if (lastSuccess is null && source.LastDownloadAt is { } dlAt
            && dlAt >= dayStart && dlAt < dayEnd)
        {
            lastSuccess = dlAt;
        }

        DateTimeOffset? lastFailed = LatestTerminalTimestamp(
            sourceJobs
                .Where(j => j.Status == DownloadJobStatus.Failed)
                .Select(j => j.CompletedAt ?? j.CreatedAt)
                .Concat(sourcePdfs.Where(p => p.Status == PdfEditionStatus.Failed).Select(p => p.DownloadedAt ?? p.DiscoveredAt ?? p.CreatedAt)),
            dayStart,
            dayEnd);

        var lastTime = lastActivity.Job?.CompletedAt ?? lastActivity.Job?.StartedAt ?? lastActivity.Job?.CreatedAt
            ?? lastActivity.Pdf?.DownloadedAt ?? lastActivity.Pdf?.DiscoveredAt ?? lastActivity.Pdf?.CreatedAt;

        Guid? latestFileId = null;
        Guid? latestJobId = latestJob?.Id ?? latestPdf?.DownloadJobId;
        if (status == DownloadMonitorStatusLabels.Success)
        {
            latestJobId = sourceJobs.FirstOrDefault(j => j.Status == DownloadJobStatus.Succeeded)?.Id
                ?? sourcePdfs.FirstOrDefault(p => p.Status == PdfEditionStatus.Downloaded)?.DownloadJobId;
            latestFileId = sourcePdfs.FirstOrDefault(p => p.Status == PdfEditionStatus.Downloaded)?.DownloadedFileId;

            if (latestFileId is null
                && latestJobId is Guid succeededJobId
                && fileIdByJobId.TryGetValue(succeededJobId, out var portalFileId))
            {
                latestFileId = portalFileId;
            }

            if (latestJobId is Guid displayJobId
                && sourceJobs.FirstOrDefault(j => j.Id == displayJobId)?.Status == DownloadJobStatus.SuccessWithAutoAiRecovery)
            {
                status = DownloadMonitorStatusLabels.SuccessWithAutoAiRecovery;
            }
            else if (IsAiRecoverySuccess(sourceJobs, latestJobId, recoveryRetryJobAttemptIds))
            {
                status = DownloadMonitorStatusLabels.SuccessByAiRecovery;
            }
        }
        else if (manualRequired)
        {
            latestJobId = ResolveInterventionJobId(lastActivity, sourceJobs, sourcePdfs);
        }

        var suggested = ManualInterventionSuggestions.GetSuggestion(
            failureCode, failureReason, source.RequiresManualAction, complianceBlocked);

        var adminInformed = latestJobId is Guid informedJobId
                            && (interventionJobIds.PendingInterventionJobIds.Contains(informedJobId)
                                || interventionJobIds.AcknowledgedInterventionJobIds.Contains(informedJobId));
        var informAdminDisabled = latestJobId is Guid disabledJobId
                                  && interventionJobIds.PendingInterventionJobIds.Contains(disabledJobId);

        Guid? aiRecoveryAttemptId = null;
        if ((status == DownloadMonitorStatusLabels.SuccessByAiRecovery
             || status == DownloadMonitorStatusLabels.SuccessWithAutoAiRecovery)
            && latestJobId is Guid recoveryJobId
            && recoveryRetryJobAttemptIds.TryGetValue(recoveryJobId, out var attemptId))
        {
            aiRecoveryAttemptId = attemptId;
        }

        return new DownloadMonitorSourceRowDto(
            source.Id,
            source.Name,
            source.SourceType.ToString(),
            source.Country,
            source.DefaultLanguage,
            status,
            lastTime,
            lastSuccess,
            lastFailed,
            latestFileId,
            latestJobId,
            failureReason,
            failureCode,
            manualRequired,
            adminInformed,
            informAdminDisabled,
            suggested,
            aiRecoveryAttemptId);
    }

    private static Guid? ResolveInterventionJobId(
        (DownloadJob? Job, PdfEditionDownload? Pdf) lastActivity,
        IReadOnlyList<DownloadJob> sourceJobs,
        IReadOnlyList<PdfEditionDownload> sourcePdfs)
    {
        if (lastActivity.Job?.Status == DownloadJobStatus.Failed)
        {
            return lastActivity.Job.Id;
        }

        if (lastActivity.Pdf?.Status == PdfEditionStatus.Failed && lastActivity.Pdf.DownloadJobId is Guid pdfJobId)
        {
            return pdfJobId;
        }

        return sourceJobs.FirstOrDefault(j => j.Status == DownloadJobStatus.Failed)?.Id
               ?? sourcePdfs.FirstOrDefault(p => p.Status == PdfEditionStatus.Failed)?.DownloadJobId;
    }

    private async Task ReconcileStaleDownloadJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var normalCutoff = now.Subtract(StaleRunningJobThreshold);
        var recoveryCutoff = now.Subtract(RecoveryRunningJobThreshold);
        var runningJobs = await db.DownloadJobs
            .Where(j => !j.IsDeleted
                        && (j.Status == DownloadJobStatus.Running || j.Status == DownloadJobStatus.Pending))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var staleJobs = runningJobs
            .Where(j =>
            {
                var started = j.StartedAt ?? j.CreatedAt;
                return IsRecoveryDownloadJob(j)
                    ? started < recoveryCutoff
                    : started < normalCutoff;
            })
            .ToList();

        if (staleJobs.Count == 0)
        {
            return;
        }

        var staleJobIds = staleJobs.Select(j => j.Id).ToList();
        var downloadedPdfJobIds = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted
                        && p.DownloadJobId != null
                        && staleJobIds.Contains(p.DownloadJobId.Value)
                        && p.Status == PdfEditionStatus.Downloaded)
            .Select(p => p.DownloadJobId!.Value)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var job in staleJobs)
        {
            if (downloadedPdfJobIds.Contains(job.Id))
            {
                job.Status = DownloadJobStatus.Succeeded;
                job.ErrorMessage = null;
            }
            else
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage ??= "Download was interrupted or timed out.";
            }

            job.CompletedAt ??= DateTimeOffset.UtcNow;
            job.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning("Reconciled {Count} stale download job(s) stuck in Running/Pending.", staleJobs.Count);

        foreach (var job in staleJobs.Where(IsRecoveryDownloadJob))
        {
            try
            {
                await recoveryOrchestrator.FinalizeAttemptAsync(ExtractRecoveryAttemptId(job), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not finalize recovery attempt for stale job {JobId}.", job.Id);
            }
        }
    }

    private static DateTimeOffset? LatestTerminalTimestamp(
        IEnumerable<DateTimeOffset> timestamps,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd)
    {
        var latest = timestamps
            .Where(t => t >= dayStart && t < dayEnd)
            .OrderByDescending(t => t)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();

        return latest;
    }

    private static Guid ExtractRecoveryAttemptId(DownloadJob job)
    {
        if (string.IsNullOrWhiteSpace(job.CorrelationId)
            || !job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(job.CorrelationId["recovery:".Length..], out var attemptId))
        {
            throw new InvalidOperationException("Download job is not linked to a recovery attempt.");
        }

        return attemptId;
    }

    /// <summary>
    /// Prefer the latest terminal job when a newer Running/Pending row is stale (e.g. API restart mid-download).
    /// </summary>
    private static bool IsRecoveryDownloadJob(DownloadJob job) =>
        !string.IsNullOrWhiteSpace(job.CorrelationId)
        && job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoveryDownloadJob(DownloadJob job, IReadOnlyDictionary<Guid, Guid> recoveryRetryJobAttemptIds) =>
        recoveryRetryJobAttemptIds.ContainsKey(job.Id) || IsRecoveryDownloadJob(job);

    private static bool IsAiRecoverySuccess(
        IReadOnlyList<DownloadJob> sourceJobs,
        Guid? succeededJobId,
        IReadOnlyDictionary<Guid, Guid> recoveryRetryJobAttemptIds)
    {
        if (succeededJobId is not Guid jobId)
        {
            return false;
        }

        if (recoveryRetryJobAttemptIds.ContainsKey(jobId))
        {
            return true;
        }

        var job = sourceJobs.FirstOrDefault(j => j.Id == jobId);
        return job is not null
               && job.Status == DownloadJobStatus.Succeeded
               && IsRecoveryDownloadJob(job, recoveryRetryJobAttemptIds);
    }

    private static DownloadJob? PickDisplayJob(
        IReadOnlyList<DownloadJob> sourceJobs,
        IReadOnlyDictionary<Guid, Guid> recoveryRetryJobAttemptIds)
    {
        if (sourceJobs.Count == 0)
        {
            return null;
        }

        var latest = sourceJobs[0];
        if (latest.Status is not (DownloadJobStatus.Running or DownloadJobStatus.Pending))
        {
            return latest;
        }

        var started = latest.StartedAt ?? latest.CreatedAt;
        var staleThreshold = IsRecoveryDownloadJob(latest, recoveryRetryJobAttemptIds)
            ? RecoveryRunningJobThreshold
            : StaleRunningJobThreshold;
        if (DateTimeOffset.UtcNow - started <= staleThreshold)
        {
            return latest;
        }

        return sourceJobs.FirstOrDefault(j => j.Status is DownloadJobStatus.Succeeded or DownloadJobStatus.Failed)
               ?? latest;
    }

    private static (DownloadJob? Job, PdfEditionDownload? Pdf) PickLatestActivity(DownloadJob? job, PdfEditionDownload? pdf)
    {
        if (job is null)
        {
            return (null, pdf);
        }

        if (pdf is null)
        {
            return (job, null);
        }

        if (job.Status is DownloadJobStatus.Running or DownloadJobStatus.Pending
            && pdf.DownloadJobId == job.Id
            && pdf.Status is PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.SkippedDuplicate)
        {
            return (null, pdf);
        }

        var jobTime = job.CompletedAt ?? job.StartedAt ?? job.CreatedAt;
        var pdfTime = pdf.DownloadedAt ?? pdf.DiscoveredAt ?? pdf.CreatedAt;
        return jobTime >= pdfTime ? (job, null) : (null, pdf);
    }

    private static string ResolveStatus(NewsSource source, DownloadJob? job, PdfEditionDownload? pdf)
    {
        if (job is not null)
        {
            return job.Status switch
            {
                DownloadJobStatus.Succeeded => DownloadMonitorStatusLabels.Success,
                DownloadJobStatus.SuccessWithAutoAiRecovery => DownloadMonitorStatusLabels.SuccessWithAutoAiRecovery,
                DownloadJobStatus.Failed => source.RequiresManualAction ? DownloadMonitorStatusLabels.ManualActionRequired : DownloadMonitorStatusLabels.Failed,
                DownloadJobStatus.FailedAfterAutoAiRecovery => DownloadMonitorStatusLabels.FailedAfterAutoAiRecovery,
                DownloadJobStatus.ManualInterventionRequired => DownloadMonitorStatusLabels.ManualActionRequired,
                DownloadJobStatus.AutoAiRecoveryAnalyzing
                    or DownloadJobStatus.AutoAiRecoveryApplying
                    or DownloadJobStatus.AutoAiRecoveryRetrying => DownloadMonitorStatusLabels.AutoAiRecoveryRunning,
                DownloadJobStatus.Running => DownloadMonitorStatusLabels.InProgress,
                DownloadJobStatus.Pending => DownloadMonitorStatusLabels.Pending,
                _ => DownloadMonitorStatusLabels.Pending
            };
        }

        if (pdf is not null)
        {
            return MapPdfStatus(pdf.Status);
        }

        if (!source.IsDownloadAllowed)
        {
            return DownloadMonitorStatusLabels.ComplianceBlocked;
        }

        return DownloadMonitorStatusLabels.NoActivity;
    }

    private static string MapJobStatus(DownloadJobStatus status) => status switch
    {
        DownloadJobStatus.Succeeded => DownloadMonitorStatusLabels.Success,
        DownloadJobStatus.SuccessWithAutoAiRecovery => DownloadMonitorStatusLabels.SuccessWithAutoAiRecovery,
        DownloadJobStatus.Failed => DownloadMonitorStatusLabels.Failed,
        DownloadJobStatus.FailedAfterAutoAiRecovery => DownloadMonitorStatusLabels.FailedAfterAutoAiRecovery,
        DownloadJobStatus.AutoAiRecoveryAnalyzing
            or DownloadJobStatus.AutoAiRecoveryApplying
            or DownloadJobStatus.AutoAiRecoveryRetrying => DownloadMonitorStatusLabels.AutoAiRecoveryRunning,
        DownloadJobStatus.Running => DownloadMonitorStatusLabels.InProgress,
        _ => DownloadMonitorStatusLabels.Pending
    };

    private static string MapPdfStatus(PdfEditionStatus status) => status switch
    {
        PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.SkippedDuplicate => DownloadMonitorStatusLabels.Success,
        PdfEditionStatus.Failed => DownloadMonitorStatusLabels.Failed,
        PdfEditionStatus.BlockedByCompliance => DownloadMonitorStatusLabels.ComplianceBlocked,
        PdfEditionStatus.NoPublicPdfAvailable => DownloadMonitorStatusLabels.NoPdfAvailable,
        _ => DownloadMonitorStatusLabels.Pending
    };

    private static string? MapPdfFailureCode(PdfEditionStatus status) => status switch
    {
        PdfEditionStatus.BlockedByCompliance => "ComplianceBlocked",
        PdfEditionStatus.NoPublicPdfAvailable => "NoPublicPdfAvailable",
        PdfEditionStatus.Failed => "PdfValidationFailed",
        _ => null
    };

    private static string? ExtractFailureCode(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (message.Contains("credential", StringComparison.OrdinalIgnoreCase))
        {
            return "InvalidCredentials";
        }

        if (message.Contains("CAPTCHA", StringComparison.OrdinalIgnoreCase))
        {
            return "CaptchaDetected";
        }

        if (message.Contains("MFA", StringComparison.OrdinalIgnoreCase) || message.Contains("OTP", StringComparison.OrdinalIgnoreCase))
        {
            return "MfaDetected";
        }

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "NetworkTimeout";
        }

        if (message.Contains("selector", StringComparison.OrdinalIgnoreCase))
        {
            return "DownloadButtonNotFound";
        }

        if (message.Contains("compliance", StringComparison.OrdinalIgnoreCase) || message.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
        {
            return "ComplianceBlocked";
        }

        if (message.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bot protection", StringComparison.OrdinalIgnoreCase))
        {
            return "AccessDenied";
        }

        if (message.Contains("No public PDF edition", StringComparison.OrdinalIgnoreCase))
        {
            return "NoPublicPdfAvailable";
        }

        return null;
    }

    private static LatestPdfLinkDto BuildPdfLink(Guid sourceId, Guid fileId, Guid? jobId, DateTimeOffset? downloadedAt) =>
        new(
            sourceId,
            fileId,
            jobId,
            $"/api/v1/operator/sources/{sourceId}/latest-pdf?inline=true",
            $"/api/v1/operator/sources/{sourceId}/latest-pdf",
            true,
            downloadedAt);

    private async Task<string?> ResolveFailureCaptureUrlAsync(
        Guid downloadJobId,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        var pdfRowUrl = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted && p.DownloadJobId == downloadJobId)
            .OrderByDescending(p => p.DiscoveredAt)
            .Select(p => p.SourceUrl)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return PdfFailureCaptureUrlResolver.Resolve(source, pdfRowUrl);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateOnly date)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return (start, start.AddDays(1));
    }

    private async Task TrySendAdminEmailAsync(
        NewsSource source,
        AdminInterventionNotification notification,
        CancellationToken cancellationToken)
    {
        var opt = schedulerOptions.Value;
        if (!mailAutomation.Value.Enabled || string.IsNullOrWhiteSpace(opt.AdminRecipientEmail))
        {
            logger.LogInformation(
                "Admin intervention notification {Id} stored without email (mail disabled or recipient missing).",
                notification.Id);
            return;
        }

        var portalUrl = string.IsNullOrWhiteSpace(opt.AdminPortalUrl)
            ? "/operator/download-monitor"
            : $"{opt.AdminPortalUrl.TrimEnd('/')}/operator/download-monitor";

        var html = $"""
            <h2>GFH MIP — Operator requested admin intervention</h2>
            <p><strong>Source:</strong> {System.Net.WebUtility.HtmlEncode(source.Name)}</p>
            <p><strong>Failure:</strong> {System.Net.WebUtility.HtmlEncode(notification.FailureReason)}</p>
            <p><strong>Suggested action:</strong> {System.Net.WebUtility.HtmlEncode(notification.SuggestedAction)}</p>
            <p><strong>Operator note:</strong> {System.Net.WebUtility.HtmlEncode(notification.OperatorNote ?? "(none)")}</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(portalUrl)}">Open Download Monitor</a></p>
            """;

        var send = await emailSender.SendAsync(
            new ReportEmailMessage([opt.AdminRecipientEmail.Trim()], "GFH MIP — Admin intervention required", html, []),
            cancellationToken).ConfigureAwait(false);

        if (!send.Success)
        {
            logger.LogWarning("Admin intervention email failed: {Error}", send.ErrorMessage ?? send.Outcome.ToString());
        }
    }
}
