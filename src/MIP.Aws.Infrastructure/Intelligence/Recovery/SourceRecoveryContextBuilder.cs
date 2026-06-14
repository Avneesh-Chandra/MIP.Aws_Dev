using System.Text;
using System.Text.Json;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class SourceRecoveryContextBuilder(
    IApplicationDbContext db,
    IFileStorageService storage,
    ILogger<SourceRecoveryContextBuilder> logger)
{
    public async Task<SourceRecoveryAnalysisContext> BuildAsync(
        NewsSource source,
        DownloadJob job,
        string failureType,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        var auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == job.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestAudit = auditLogs.FirstOrDefault();
        var (screenshotPath, htmlSnapshotPath) = PortalAuditArtifactResolver.ResolvePaths(auditLogs);
        var playwrightLog = BuildPlaywrightLogExcerpt(auditLogs);
        var browserConsole = auditLogs
            .Where(a => a.EventKind?.Contains("Console", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Message)
            .FirstOrDefault();
        var networkLog = auditLogs
            .Where(a => a.EventKind?.Contains("Network", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Message)
            .FirstOrDefault();

        string? htmlSnapshot = null;
        if (htmlSnapshotPath is not null)
        {
            try
            {
                var bytes = await storage.ReadAsync(htmlSnapshotPath, cancellationToken).ConfigureAwait(false);
                htmlSnapshot = Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read HTML snapshot for recovery context.");
            }
        }

        var lastSuccessVersion = await db.SourceConfigurationVersions.AsNoTracking()
            .Where(v => !v.IsDeleted
                        && v.NewsSourceId == source.Id
                        && v.Status == SourceConfigurationVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastSuccessJob = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.NewsSourceId == source.Id && j.Status == DownloadJobStatus.Succeeded)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var operatorNotes = await db.DownloadOperatorNotes.AsNoTracking()
            .Where(n => !n.IsDeleted && n.DownloadJobId == job.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => n.Note)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var adminNotes = await db.AdminInterventionNotifications.AsNoTracking()
            .Where(n => !n.IsDeleted && n.NewsSourceId == source.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => n.OperatorNote ?? n.SuggestedAction)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var knowledge = await db.SourceRecoveryKnowledgeEntries.AsNoTracking()
            .Where(k => !k.IsDeleted && k.FailureType == failureType)
            .OrderByDescending(k => k.SuccessCount)
            .Take(5)
            .Select(k => new SourceRecoveryKnowledgeHint(
                k.FailureType,
                k.FieldName,
                k.OldSelector,
                k.NewSelector,
                k.SuccessCount))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastPdfMeta = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted && p.NewsSourceId == source.Id && p.Status == PdfEditionStatus.Downloaded)
            .OrderByDescending(p => p.DownloadedAt)
            .Select(p => new { p.EditionDate, p.DownloadedAt, p.FileSizeBytes, p.SourceUrl })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SourceRecoveryAnalysisContext(
            source.Id,
            source.Name,
            job.Id,
            failureType,
            failureCode ?? string.Empty,
            job.ErrorMessage ?? "Download failed.",
            source.EditionUrl ?? source.BaseUrl,
            source.EditionUrl,
            source.LoginUrl,
            job.RetryCount,
            job.CompletedAt ?? job.StartedAt ?? job.CreatedAt,
            SourceRecoveryConfigurationSnapshot.FromEntity(source).ToJson(),
            lastSuccessVersion?.JsonConfiguration,
            playwrightLog,
            browserConsole,
            networkLog,
            htmlSnapshot,
            screenshotPath,
            htmlSnapshotPath,
            lastPdfMeta is null ? null : JsonSerializer.Serialize(lastPdfMeta),
            operatorNotes,
            adminNotes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList(),
            knowledge);
    }

    private static string? BuildPlaywrightLogExcerpt(IReadOnlyList<PortalDownloadAuditLog> logs)
    {
        var sb = new StringBuilder();
        foreach (var log in logs.Take(20))
        {
            if (!string.IsNullOrWhiteSpace(log.EventKind) || !string.IsNullOrWhiteSpace(log.Message))
            {
                sb.Append('[').Append(log.EventKind ?? "Event").Append("] ")
                    .AppendLine(log.Message ?? string.Empty);
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
