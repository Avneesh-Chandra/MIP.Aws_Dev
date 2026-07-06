using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorDailyStatusEmailService(
    IApplicationDbContext db,
    IOperatorDownloadMonitorService monitorService,
    IReportEmailSender emailSender,
    IDownloadMonitorStatusSummaryService summaryService,
    IMailSettingsService mailSettings,
    ILogger<DownloadMonitorDailyStatusEmailService> logger) : IDownloadMonitorDailyStatusEmailService
{
    public async Task<bool> SendDailyStatusEmailAsync(
        DateOnly? monitorDate,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? recipientOverride = null,
        string? executiveSummaryPrefix = null)
    {
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        if (!scheduler.StatusEmailEnabled)
        {
            logger.LogInformation("Download monitor status email skipped (StatusEmailEnabled=false).");
            return false;
        }

        var recipients = recipientOverride is { Count: > 0 }
            ? recipientOverride
            : ParseRecipients(scheduler.StatusEmailRecipient);
        if (recipients.Count == 0)
        {
            logger.LogWarning("Download monitor status email skipped: StatusEmailRecipient is not configured.");
            return false;
        }

        var date = monitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (await DownloadMonitorStatusEmailGuard.ShouldThrottleAdHocDailyStatusEmailAsync(db, date, cancellationToken)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Download monitor status email skipped for {Date}: a status email was sent recently.",
                date);
            return false;
        }

        var monitor = await monitorService
            .GetMonitorAsync(date, skipReconciliation: false, cancellationToken)
            .ConfigureAwait(false);
        var portalBase = ResolvePortalBaseUrl(scheduler.AdminPortalUrl);
        var summary = await summaryService.BuildSummaryAsync(monitor, cancellationToken).ConfigureAwait(false);
        var failureContexts = await LoadFailureContextsAsync(monitor, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(executiveSummaryPrefix))
        {
            summary = string.IsNullOrWhiteSpace(summary)
                ? executiveSummaryPrefix
                : $"{executiveSummaryPrefix}\n\n{summary}";
        }

        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(
            monitor,
            portalBase,
            summary,
            pendingRecoveryNotices: null,
            adminRecipientEmail: ResolveAdminRecipientEmail(scheduler),
            failureContextsByJobId: failureContexts);
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(date);

        return await SendHtmlAsync(recipients, subject, html, date, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SendRecoveryFollowUpEmailAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        if (!scheduler.StatusEmailEnabled)
        {
            logger.LogInformation("Download monitor recovery follow-up email skipped (StatusEmailEnabled=false).");
            return false;
        }

        var recipients = ParseRecipients(scheduler.StatusEmailRecipient);
        if (recipients.Count == 0)
        {
            logger.LogWarning("Download monitor recovery follow-up email skipped: StatusEmailRecipient is not configured.");
            return false;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var sections = await BuildRecoveryFollowUpSectionsAsync(batchStartedAt, cancellationToken).ConfigureAwait(false);
        if (sections.Count == 0)
        {
            logger.LogInformation(
                "Download monitor recovery follow-up email skipped for batch {BatchStartedAt:u}: no recovery runs found.",
                batchStartedAt);
            return false;
        }

        var portalBase = ResolvePortalBaseUrl(scheduler.AdminPortalUrl);
        var html = DownloadMonitorRecoveryFollowUpEmailHtmlBuilder.Build(
            monitorDate,
            portalBase,
            sections,
            ResolveAdminRecipientEmail(scheduler));
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildRecoveryFollowUpSubject(monitorDate);

        return await SendHtmlAsync(recipients, subject, html, monitorDate, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SendInitialBatchStatusEmailAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        if (!scheduler.StatusEmailEnabled)
        {
            return false;
        }

        var recipients = ParseRecipients(scheduler.StatusEmailRecipient);
        if (recipients.Count == 0)
        {
            return false;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var monitor = await monitorService
            .GetMonitorAsync(monitorDate, skipReconciliation: false, cancellationToken)
            .ConfigureAwait(false);
        var portalBase = ResolvePortalBaseUrl(scheduler.AdminPortalUrl);
        var summary = await summaryService.BuildSummaryAsync(monitor, cancellationToken).ConfigureAwait(false);

        var sourceIds = await LoadMonitoredSourceIdsAsync(cancellationToken).ConfigureAwait(false);
        var pending = await DownloadMonitorBatchOutcomeHelper.GetPendingAutoRecoverySourcesForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false);

        var pendingNotices = pending
            .Select(p => $"{p.SourceName} auto-recovery is still in progress ({FormatRecoveryStatus(p.Status)}). A follow-up email will be sent when it finishes.")
            .ToList();

        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(
            monitor,
            portalBase,
            summary,
            pendingNotices,
            adminRecipientEmail: ResolveAdminRecipientEmail(scheduler),
            failureContextsByJobId: await LoadFailureContextsAsync(monitor, cancellationToken).ConfigureAwait(false));

        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        return await SendHtmlAsync(recipients, subject, html, monitorDate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<DownloadMonitorRecoveryFollowUpEmailHtmlBuilder.RecoveryFollowUpSourceSection>>
        BuildRecoveryFollowUpSectionsAsync(
            DateTimeOffset batchStartedAt,
            CancellationToken cancellationToken)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);
        var sourceIds = await LoadMonitoredSourceIdsAsync(cancellationToken).ConfigureAwait(false);
        if (sourceIds.Count == 0)
        {
            return [];
        }

        var runs = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted
                        && sourceIds.Contains(r.NewsSourceId)
                        && r.CreatedAt >= notBefore
                        && r.CompletedAt != null)
            .Join(
                db.NewsSources.AsNoTracking().Where(s => !s.IsDeleted),
                r => r.NewsSourceId,
                s => s.Id,
                (r, s) => new { Run = r, SourceName = s.Name })
            .OrderBy(x => x.SourceName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return runs
            .Select(x => new DownloadMonitorRecoveryFollowUpEmailHtmlBuilder.RecoveryFollowUpSourceSection(
                x.SourceName,
                x.Run.Status,
                x.Run.Status == AutoAiRecoveryRunStatus.CompletedSuccess,
                x.Run.ResultSummary,
                x.Run.SuccessfulOptionTitle,
                x.Run.SuggestionsTried,
                AutoAiRecoveryTimelineJson.Deserialize(x.Run.TimelineJson)))
            .ToList();
    }

    private async Task<List<Guid>> LoadMonitoredSourceIdsAsync(CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sources.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).Select(s => s.Id).ToList();
    }

    private async Task<bool> SendHtmlAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string html,
        DateOnly logDate,
        CancellationToken cancellationToken)
    {
        var send = await emailSender.SendAsync(
            new ReportEmailMessage(recipients, subject, html, []),
            cancellationToken).ConfigureAwait(false);

        if (send.Success)
        {
            logger.LogInformation(
                "Download monitor email sent ({Subject}) to {Recipient} for {Date}.",
                subject,
                string.Join(", ", recipients),
                logDate);
            return true;
        }

        logger.LogWarning(
            "Download monitor email failed ({Subject}) for {Date}: {Error}",
            subject,
            logDate,
            send.ErrorMessage ?? send.Outcome.ToString());
        return false;
    }

    private static string ResolveAdminRecipientEmail(EffectiveSchedulerMailSettings scheduler) =>
        string.IsNullOrWhiteSpace(scheduler.AdminRecipientEmail)
            ? string.Empty
            : scheduler.AdminRecipientEmail.Trim();

    private async Task<IReadOnlyDictionary<Guid, DownloadMonitorEmailFailureContext>> LoadFailureContextsAsync(
        DownloadMonitorDto monitor,
        CancellationToken cancellationToken)
    {
        var failedJobIds = monitor.Sources
            .Where(s => s.ManualInterventionRequired && s.LatestDownloadJobId is Guid)
            .Select(s => s.LatestDownloadJobId!.Value)
            .Distinct()
            .ToList();
        if (failedJobIds.Count == 0)
        {
            return new Dictionary<Guid, DownloadMonitorEmailFailureContext>();
        }

        var runs = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && failedJobIds.Contains(r.FailedDownloadJobId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var contexts = new Dictionary<Guid, DownloadMonitorEmailFailureContext>();
        foreach (var jobId in failedJobIds)
        {
            var run = runs.FirstOrDefault(r => r.FailedDownloadJobId == jobId);
            if (run is null)
            {
                continue;
            }

            var timeline = AutoAiRecoveryTimelineJson.Deserialize(run.TimelineJson)
                .Select(step => new DownloadMonitorEmailTimelineStep(step.Step, step.Detail, step.Timestamp))
                .ToList();
            contexts[jobId] = new DownloadMonitorEmailFailureContext(run.ResultSummary, timeline);
        }

        return contexts;
    }

    private static string FormatRecoveryStatus(AutoAiRecoveryRunStatus status) => status switch
    {
        AutoAiRecoveryRunStatus.Analyzing => "analyzing failure",
        AutoAiRecoveryRunStatus.ApplyingCandidate => "applying AI-suggested fix",
        AutoAiRecoveryRunStatus.RetryingDownload => "retrying download",
        AutoAiRecoveryRunStatus.Queued => "queued",
        _ => status.ToString()
    };

    private static string ResolvePortalBaseUrl(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? string.Empty
            : configured.TrimEnd('/');

    private static IReadOnlyList<string> ParseRecipients(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
