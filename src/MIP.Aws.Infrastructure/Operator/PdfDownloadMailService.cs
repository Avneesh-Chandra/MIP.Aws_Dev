using System.Globalization;
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

        var recipients = scheduler.SelectedPdfDownloadMailRecipients;
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

        if (!await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForConfiguredMailAsync(
                db,
                sourceIds,
                batchStartedAt,
                scheduler.PdfDownloadMailCompletionMode,
                cancellationToken)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "PDF Download mail skipped for {BatchStartedAt:u}: the configured completion condition is not met.",
                batchStartedAt);
            return false;
        }

        // Keep file selection consistent with IsSourceSuccessfulAsync: a PDF downloaded today
        // counts even when the publisher's EditionDate is stored in a different timezone.
        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var editionDayStart = new DateTimeOffset(editionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var editionDayEnd = editionDayStart.AddDays(1);
        var sourceNames = await db.NewsSources.AsNoTracking()
            .Where(source => !source.IsDeleted && sourceIds.Contains(source.Id))
            .Select(source => new { source.Id, source.Name })
            .ToDictionaryAsync(source => source.Id, source => source.Name, cancellationToken)
            .ConfigureAwait(false);

        var editionRows = await db.PdfEditionDownloads.AsNoTracking()
            .Where(edition => !edition.IsDeleted
                              && sourceIds.Contains(edition.NewsSourceId)
                              && edition.DownloadedFileId != null
                              && (edition.EditionDate == editionDate
                                  || (edition.DownloadedAt >= editionDayStart
                                      && edition.DownloadedAt < editionDayEnd))
                              && (edition.Status == PdfEditionStatus.Downloaded
                                  || edition.Status == PdfEditionStatus.SkippedDuplicate
                                  || edition.Status == PdfEditionStatus.Validated))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var publicCandidates = editionRows
            .Where(edition => edition.DownloadedFileId is Guid
                              && sourceNames.ContainsKey(edition.NewsSourceId))
            .Select(edition => new Candidate(
                edition.NewsSourceId,
                sourceNames[edition.NewsSourceId],
                edition.DownloadedFileId!.Value,
                BuildEditionFileName(sourceNames[edition.NewsSourceId], editionDate),
                edition.FileSizeBytes ?? 0,
                edition.DownloadedAt ?? edition.CreatedAt))
            .ToList();

        // Licensed portal downloads (for example PressReader) are recorded as successful
        // DownloadJobs with DownloadedFiles, rather than PdfEditionDownloads.
        var portalJobs = await db.DownloadJobs.AsNoTracking()
            .Where(job => !job.IsDeleted
                          && sourceIds.Contains(job.NewsSourceId)
                          && (job.Status == DownloadJobStatus.Succeeded
                              || job.Status == DownloadJobStatus.SuccessWithAutoAiRecovery)
                          && job.CompletedAt >= editionDayStart
                          && job.CompletedAt < editionDayEnd)
            .Select(job => new
            {
                job.Id,
                job.NewsSourceId,
                job.CompletedAt,
                job.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var portalJobIds = portalJobs.Select(job => job.Id).ToList();
        var portalFiles = portalJobIds.Count == 0
            ? []
            : await db.DownloadedFiles.AsNoTracking()
                .Where(file => !file.IsDeleted && portalJobIds.Contains(file.DownloadJobId))
                .Select(file => new
                {
                    file.Id,
                    file.DownloadJobId,
                    file.SizeBytes
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var portalCandidates = portalJobs
            .Join(
                portalFiles,
                job => job.Id,
                file => file.DownloadJobId,
                (job, file) => new { job, file })
            .Where(item => sourceNames.ContainsKey(item.job.NewsSourceId))
            .Select(item => new Candidate(
                item.job.NewsSourceId,
                sourceNames[item.job.NewsSourceId],
                item.file.Id,
                BuildEditionFileName(sourceNames[item.job.NewsSourceId], editionDate),
                item.file.SizeBytes,
                item.job.CompletedAt ?? item.job.CreatedAt))
            .ToList();

        var candidates = publicCandidates
            .Concat(portalCandidates)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .ToList();

        var selected = candidates
            .GroupBy(candidate => candidate.SourceId)
            .Select(group => group.First())
            .ToDictionary(candidate => candidate.SourceId);

        var fileIds = selected.Values.Select(candidate => candidate.FileId).ToList();
        var storedFileIds = await db.DownloadedFiles.AsNoTracking()
            .Where(file => !file.IsDeleted && fileIds.Contains(file.Id))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (storedFileIds.Count != fileIds.Count
            && scheduler.PdfDownloadMailCompletionMode == MailCompletionMode.AllSourcesSuccessful)
        {
            logger.LogInformation(
                "PDF Download mail skipped for {BatchStartedAt:u}: one or more PDF files are unavailable.",
                batchStartedAt);
            return false;
        }
        var storedFileIdSet = storedFileIds.ToHashSet();
        selected = selected
            .Where(pair => storedFileIdSet.Contains(pair.Value.FileId))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (scheduler.PdfDownloadMailCompletionMode == MailCompletionMode.AllSourcesSuccessful
            && selected.Count != sourceIds.Count)
        {
            logger.LogInformation(
                "PDF Download mail skipped for {BatchStartedAt:u}: not every successful source has a stored PDF edition.",
                batchStartedAt);
            return false;
        }

        var configuredSubject = scheduler.PdfDownloadMailSubject?.Trim();
        var subject = string.IsNullOrWhiteSpace(configuredSubject)
            || string.Equals(configuredSubject, "GFH PDF Download", StringComparison.OrdinalIgnoreCase)
            ? $"GFH Media Intelligence | Daily Newspaper Editions | {editionDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}"
            : configuredSubject.Replace(
                "{date}",
                editionDate.ToString("yyyy-MM-dd"),
                StringComparison.OrdinalIgnoreCase);
        if (!await PdfDownloadMailGuard.TryClaimAsync(
                db,
                batchStartedAt,
                subject,
                cancellationToken,
                allowResend: bypassSchedule)
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

        foreach (var sourceId in sourceIds.Where(sourceId => !selected.ContainsKey(sourceId)))
        {
            items.Add(new PdfDownloadMailItem(
                sourceNames[sourceId],
                "Not Available",
                editionDate,
                0,
                string.Empty,
                string.Empty,
                IsAvailable: false));
        }

        var allUrl = tokenFiles.Count == 0
            ? string.Empty
            : OperatorLatestPdfLinks.BuildPublicPdfDownloadUrl(
                portalBase,
                linkService.CreateToken(tokenFiles, expiresAt),
                inline: false);
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

    private static bool IsScheduleDue(string timeUtc) =>
        TimeOnly.TryParse(timeUtc, out var configuredTime)
        && TimeOnly.FromDateTime(DateTime.UtcNow) >= configuredTime;

    private static string BuildEditionFileName(string sourceName, DateOnly editionDate)
    {
        var baseName = $"{sourceName} - {editionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(
            baseName
                .Select(character => invalidCharacters.Contains(character) ? '-' : character)
                .ToArray())
            .Trim();
        return $"{(string.IsNullOrWhiteSpace(safeName) ? "newspaper-edition" : safeName)}.pdf";
    }

    private sealed record Candidate(
        Guid SourceId,
        string SourceName,
        Guid FileId,
        string FileName,
        long SizeBytes,
        DateTimeOffset OccurredAt);
}
