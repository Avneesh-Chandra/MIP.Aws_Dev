using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class PdfDownloadMailService(
    IApplicationDbContext db,
    IReportEmailSender emailSender,
    IMailSettingsService mailSettings,
    IPdfDownloadMailLinkService linkService,
    ILogger<PdfDownloadMailService> logger) : IPdfDownloadMailService
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    public async Task<bool> SendForLatestBatchAsync(
        CancellationToken cancellationToken,
        bool bypassSchedule = false)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var batch = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(run => !run.IsDeleted && run.StartedAt >= todayStart)
            .OrderByDescending(run => run.StartedAt)
            .Select(run => run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return batch == default
            ? false
            : await SendForBatchAsync(batch, cancellationToken, bypassSchedule).ConfigureAwait(false);
    }

    public async Task<bool> SendForBatchAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken,
        bool bypassSchedule = false)
    {
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        if (!scheduler.PdfDownloadMailEnabled)
        {
            logger.LogInformation("PDF Download mail skipped: feature is disabled.");
            return false;
        }

        if (!bypassSchedule && !IsScheduleDue(scheduler.PdfDownloadMailTimeUtc))
        {
            logger.LogInformation(
                "PDF Download mail deferred until the configured UTC time {TimeUtc}.",
                scheduler.PdfDownloadMailTimeUtc);
            return false;
        }

        var recipients = ParseRecipients(scheduler.PdfDownloadMailRecipient);
        if (recipients.Count == 0)
        {
            logger.LogWarning("PDF Download mail skipped: no recipients are configured.");
            return false;
        }

        var portalBase = OperatorLatestPdfLinks.NormalizePortalBaseUrl(scheduler.AdminPortalUrl);
        if (string.IsNullOrWhiteSpace(portalBase))
        {
            logger.LogWarning("PDF Download mail skipped: AdminPortalUrl is not configured.");
            return false;
        }

        var sourceIds = await LoadMonitoredSourceIdsAsync(cancellationToken).ConfigureAwait(false);
        if (sourceIds.Count == 0)
        {
            logger.LogWarning("PDF Download mail skipped: no monitored sources are configured.");
            return false;
        }

        if (await DownloadMonitorBatchOutcomeHelper.HasPendingAutoRecoveryForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "PDF Download mail deferred for {BatchStartedAt:u}: auto-recovery is still in progress.",
                batchStartedAt);
            return false;
        }

        foreach (var sourceId in sourceIds)
        {
            if (!await DownloadMonitorBatchOutcomeHelper.IsSourceSuccessfulAsync(
                    db,
                    sourceId,
                    batchStartedAt,
                    cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation(
                    "PDF Download mail skipped for {BatchStartedAt:u}: source {SourceId} is not successful.",
                    batchStartedAt,
                    sourceId);
                return false;
            }
        }

        var editionDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var candidates = await db.PdfEditionDownloads.AsNoTracking()
            .Where(edition => !edition.IsDeleted
                              && sourceIds.Contains(edition.NewsSourceId)
                              && edition.DownloadedFileId != null
                              && edition.EditionDate == editionDate
                              && (edition.Status == PdfEditionStatus.Downloaded
                                  || edition.Status == PdfEditionStatus.SkippedDuplicate
                                  || edition.Status == PdfEditionStatus.Validated))
            .Join(
                db.NewsSources.AsNoTracking().Where(source => !source.IsDeleted),
                edition => edition.NewsSourceId,
                source => source.Id,
                (edition, source) => new Candidate(
                    edition.NewsSourceId,
                    source.Name,
                    edition.DownloadedFileId!.Value,
                    edition.FileName,
                    edition.FileSizeBytes ?? 0,
                    edition.DownloadedAt ?? edition.CreatedAt))
            .OrderByDescending(candidate => candidate.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var selected = candidates
            .GroupBy(candidate => candidate.SourceId)
            .Select(group => group.First())
            .ToDictionary(candidate => candidate.SourceId);
        if (selected.Count != sourceIds.Count)
        {
            logger.LogInformation(
                "PDF Download mail skipped for {BatchStartedAt:u}: not every successful source has a stored PDF edition.",
                batchStartedAt);
            return false;
        }

        var fileIds = selected.Values.Select(candidate => candidate.FileId).ToList();
        var storedFileIds = await db.DownloadedFiles.AsNoTracking()
            .Where(file => !file.IsDeleted && fileIds.Contains(file.Id))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (storedFileIds.Count != fileIds.Count)
        {
            logger.LogInformation(
                "PDF Download mail skipped for {BatchStartedAt:u}: one or more PDF files are unavailable.",
                batchStartedAt);
            return false;
        }

        var subject = string.IsNullOrWhiteSpace(scheduler.PdfDownloadMailSubject)
            ? "GFH PDF Download"
            : scheduler.PdfDownloadMailSubject.Trim().Replace(
                "{date}",
                editionDate.ToString("yyyy-MM-dd"),
                StringComparison.OrdinalIgnoreCase);
        if (!await PdfDownloadMailGuard.TryClaimAsync(db, batchStartedAt, subject, cancellationToken)
                .ConfigureAwait(false))
        {
            logger.LogInformation(
                "PDF Download mail already sent or claimed for {BatchStartedAt:u}.",
                batchStartedAt);
            return false;
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(LinkLifetime);
        var tokenFiles = selected.Values
            .OrderBy(candidate => candidate.SourceName)
            .Select(candidate => new PdfDownloadMailLinkFile(
                candidate.SourceId,
                candidate.FileId,
                candidate.FileName))
            .ToList();
        var allToken = linkService.CreateToken(tokenFiles, expiresAt);
        var items = new List<PdfDownloadMailItem>(tokenFiles.Count);
        foreach (var file in tokenFiles)
        {
            var token = linkService.CreateToken([file], expiresAt);
            var viewUrl = OperatorLatestPdfLinks.BuildPublicPdfDownloadUrl(portalBase, token, inline: true);
            var downloadUrl = OperatorLatestPdfLinks.BuildPublicPdfDownloadUrl(portalBase, token, inline: false);
            var candidate = selected[file.SourceId];
            items.Add(new PdfDownloadMailItem(
                candidate.SourceName,
                candidate.FileName,
                editionDate,
                candidate.SizeBytes,
                viewUrl,
                downloadUrl));
        }

        var allUrl = OperatorLatestPdfLinks.BuildPublicPdfDownloadUrl(portalBase, allToken, inline: false);
        var logoUrl = $"{portalBase}/_content/MIP.Aws.Blazor/images/gfh-logo.svg";
        var html = PdfDownloadMailHtmlBuilder.Build(items, allUrl, logoUrl);

        try
        {
            var result = await emailSender.SendAsync(
                    new ReportEmailMessage(recipients, subject, html, []),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Success)
            {
                logger.LogInformation(
                    "PDF Download mail sent for {BatchStartedAt:u} to {Recipients}. Links expire at {ExpiresAt:u}.",
                    batchStartedAt,
                    string.Join(", ", recipients),
                    expiresAt);
                return true;
            }

            await PdfDownloadMailGuard.ReleaseAsync(db, batchStartedAt, subject, cancellationToken)
                .ConfigureAwait(false);
            logger.LogWarning(
                "PDF Download mail failed for {BatchStartedAt:u}: {Error}.",
                batchStartedAt,
                result.ErrorMessage ?? result.Outcome.ToString());
            return false;
        }
        catch
        {
            await PdfDownloadMailGuard.ReleaseAsync(db, batchStartedAt, subject, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    private async Task<List<Guid>> LoadMonitoredSourceIdsAsync(CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources.AsNoTracking()
            .Where(source => !source.IsDeleted && source.IsEnabled)
            .OrderBy(source => source.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sources
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .Select(source => source.Id)
            .ToList();
    }

    private static IReadOnlyList<string> ParseRecipients(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static bool IsScheduleDue(string timeUtc) =>
        TimeOnly.TryParse(timeUtc, out var configuredTime)
        && TimeOnly.FromDateTime(DateTime.UtcNow) >= configuredTime;

    private sealed record Candidate(
        Guid SourceId,
        string SourceName,
        Guid FileId,
        string FileName,
        long SizeBytes,
        DateTimeOffset OccurredAt);
}
