using System.Text.Json;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.PdfEdition;
using MIP.Aws.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

/// <summary>Persists recovery analysis and delegates option generation to <see cref="IAISourceRecoveryService"/>.</summary>
public sealed class SourceRecoveryService(
    IApplicationDbContext db,
    IAISourceRecoveryService ai,
    SourceRecoveryContextBuilder contextBuilder,
    IPdfEditionFailureArtifactService failureArtifacts,
    PortalArtifactUrlBuilder artifactUrls,
    IAuditService audit) : ISourceRecoveryAnalysisService
{
    private static readonly JsonSerializerOptions AnalysisJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<SourceRecoveryAnalysisDto> AnalyzeAndPersistAsync(
        Guid downloadJobId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .Include(j => j.NewsSource)
            .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Download job was not found.");

        var source = job.NewsSource;
        await EnsureFailureArtifactsIfMissingAsync(job, source, cancellationToken).ConfigureAwait(false);

        var auditRow = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == downloadJobId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var failureCode = auditRow?.FailureCode;
        var failureType = SourceRecoveryFailureTypeMapper.Map(failureCode, job.ErrorMessage);
        var context = await contextBuilder.BuildAsync(source, job, failureType, failureCode, cancellationToken)
            .ConfigureAwait(false);

        var options = await ai.GenerateRecoveryOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        var recommended = ai.RecommendBestOptionIndex(options);

        var attempt = new SourceRecoveryAttempt
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            DownloadJobId = job.Id,
            FailureType = failureType,
            FailureCode = failureCode,
            FailureMessage = job.ErrorMessage ?? "Download failed.",
            AnalysisJson = JsonSerializer.Serialize(new { options, recommended }, AnalysisJsonOptions),
            Status = SourceRecoveryAttemptStatus.AnalysisGenerated,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorUserId
        };

        db.SourceRecoveryAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.RecordAdminActionAsync(
            SourceRecoveryAuditEvents.AnalysisGenerated,
            "SourceRecoveryAttempt",
            attempt.Id.ToString(),
            new { source.Name, failureType, optionCount = options.Count },
            cancellationToken).ConfigureAwait(false);

        return await ToAnalysisDtoAsync(
            attempt,
            source.Name,
            job,
            options,
            recommended,
            context,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SourceRecoveryAnalysisDto?> GetAnalysisAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await db.SourceRecoveryAttempts.AsNoTracking()
            .Include(a => a.NewsSource)
            .Include(a => a.DownloadJob)
            .FirstOrDefaultAsync(a => a.Id == attemptId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (attempt is null)
        {
            return null;
        }

        var (options, recommended) = DeserializeAnalysis(attempt.AnalysisJson);
        var auditRow = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == attempt.DownloadJobId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var context = attempt.DownloadJob is null
            ? null
            : await contextBuilder.BuildAsync(
                attempt.NewsSource,
                attempt.DownloadJob,
                attempt.FailureType,
                attempt.FailureCode,
                cancellationToken).ConfigureAwait(false);

        return await ToAnalysisDtoAsync(
            attempt,
            attempt.NewsSource.Name,
            attempt.DownloadJob,
            options,
            recommended,
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureFailureArtifactsIfMissingAsync(
        DownloadJob job,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        if (job.Status is not DownloadJobStatus.Failed)
        {
            return;
        }

        var pdfRowUrl = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted && p.DownloadJobId == job.Id)
            .OrderByDescending(p => p.DiscoveredAt)
            .Select(p => p.SourceUrl)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var captureUrl = PdfFailureCaptureUrlResolver.Resolve(source, pdfRowUrl);

        if (string.IsNullOrWhiteSpace(captureUrl))
        {
            return;
        }

        await failureArtifacts.EnsureFailureArtifactsAsync(
            source.Id,
            job.Id,
            captureUrl,
            job.ErrorMessage ?? "Download failed.",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SourceRecoveryAnalysisDto> ToAnalysisDtoAsync(
        SourceRecoveryAttempt attempt,
        string sourceName,
        DownloadJob? job,
        IReadOnlyList<SourceRecoveryOptionDto> options,
        int recommended,
        SourceRecoveryAnalysisContext? context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PortalDownloadAuditLog> auditLogs;
        if (job is null)
        {
            auditLogs = Array.Empty<PortalDownloadAuditLog>();
        }
        else
        {
            auditLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
                .Where(a => !a.IsDeleted && a.DownloadJobId == job.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var (screenshot, html) = await artifactUrls.BuildUrlsAsync(auditLogs, cancellationToken).ConfigureAwait(false);

        return new SourceRecoveryAnalysisDto(
            attempt.Id,
            attempt.NewsSourceId,
            sourceName,
            attempt.DownloadJobId,
            attempt.FailureType,
            attempt.FailureMessage,
            context?.SourceUrl,
            job?.CompletedAt ?? job?.StartedAt,
            job?.RetryCount ?? 0,
            screenshot,
            html,
            options,
            recommended >= 0 ? recommended : null,
            context is null ? Array.Empty<string>() : ExtractFindings(attempt.AnalysisJson, "screenshotFindings"),
            context is null ? Array.Empty<string>() : ExtractFindings(attempt.AnalysisJson, "htmlFindings"),
            ai.IsEnabled);
    }

    private static (IReadOnlyList<SourceRecoveryOptionDto> Options, int Recommended) DeserializeAnalysis(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var options = root.TryGetProperty("options", out var o)
                ? SourceRecoveryJsonParser.ParseOptions(json)
                : SourceRecoveryJsonParser.ParseOptions($"{{\"options\":{json}}}");
            var recommended = root.TryGetProperty("recommended", out var r)
                              && r.ValueKind == JsonValueKind.Number
                              && r.TryGetInt32(out var i)
                ? i
                : -1;
            return (options, recommended);
        }
        catch
        {
            return (Array.Empty<SourceRecoveryOptionDto>(), -1);
        }
    }

    private static IReadOnlyList<string> ExtractFindings(string json, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return SourceRecoveryJsonParser.ParseStringArray(doc.RootElement, property);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
