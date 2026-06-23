using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Jobs;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Compliance;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfEditionDownloadService(
    IServiceScopeFactory scopeFactory,
    IPdfEditionDiscoveryService discovery,
    PdfEditionValidator validator,
    PdfEditionContentFetcher contentFetcher,
    IHttpClientFactory httpClientFactory,
    IPdfEditionFailureArtifactService failureArtifacts,
    IPdfEditionDownloadProgressTracker progress,
    IPublisherComplianceGate complianceGate,
    IFileStorageService fileStorage,
    IAuditService audit,
    IIntelligenceJobScheduler intelligenceScheduler,
    IOptions<StorageOptions> storageOptions,
    ILogger<PdfEditionDownloadService> logger) : IPdfEditionDownloadService
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private Guid? _boundDownloadJobId;
    private DownloadJobTrigger? _downloadJobTrigger;

    private DownloadJobTrigger EffectiveDownloadJobTrigger =>
        _downloadJobTrigger ?? DownloadExecutionContext.CurrentTrigger;

    public Task<PdfEditionDownloadOutcome> DiscoverOnlyAsync(Guid newsSourceId, CancellationToken cancellationToken) =>
        RunAsync(newsSourceId, download: false, enqueueOcr: false, cancellationToken);

    public Task<PdfEditionDownloadOutcome> DownloadTodayAsync(Guid newsSourceId, bool enqueueOcr, CancellationToken cancellationToken) =>
        RunAsync(newsSourceId, download: true, enqueueOcr, cancellationToken);

    public Task<PdfEditionDownloadOutcome> DownloadManualAsync(
        Guid newsSourceId,
        string manualUrl,
        bool saveAsDiscoveryPageUrl,
        bool enqueueOcr,
        CancellationToken cancellationToken) =>
        RunManualAsync(newsSourceId, manualUrl, saveAsDiscoveryPageUrl, enqueueOcr, cancellationToken);

    public async Task<PdfEditionDownloadOutcome> ExecuteDownloadJobAsync(Guid downloadJobId, CancellationToken cancellationToken)
    {
        var job = await WithDbContextAsync(async (db, ct) =>
            await db.DownloadJobs.AsNoTracking()
                .Include(j => j.NewsSource)
                .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, ct)
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Download job was not found.");

        if (job.NewsSource is null)
        {
            throw new InvalidOperationException("Download job is missing its news source.");
        }

        if (!job.NewsSource.PdfDiscoveryEnabled
            || job.NewsSource.SourceType is not (NewsSourceType.PublicPdf or NewsSourceType.PublicHtml))
        {
            throw new InvalidOperationException("This job is not a public PDF edition download.");
        }

        _boundDownloadJobId = downloadJobId;
        _downloadJobTrigger = job.Trigger;
        try
        {
            return await RunAsync(job.NewsSourceId, download: true, enqueueOcr: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await FailBoundDownloadJobAsync(downloadJobId, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _boundDownloadJobId = null;
            _downloadJobTrigger = null;
        }
    }

    public Task<PdfEditionDownloadOutcome?> GetLatestAsync(Guid newsSourceId, CancellationToken cancellationToken) =>
        WithDbContextAsync(async (db, ct) =>
        {
            var row = await db.PdfEditionDownloads.AsNoTracking()
                .Where(x => !x.IsDeleted && x.NewsSourceId == newsSourceId && x.Status == PdfEditionStatus.Downloaded)
                .OrderByDescending(x => x.EditionDate)
                .ThenByDescending(x => x.DownloadedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return row is null ? null : MapOutcome(row, null);
        }, cancellationToken);

    private async Task<PdfEditionDownloadOutcome> RunManualAsync(
        Guid newsSourceId,
        string manualUrl,
        bool saveAsDiscoveryPageUrl,
        bool enqueueOcr,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(manualUrl) || !Uri.TryCreate(manualUrl.Trim(), UriKind.Absolute, out var manualUri)
            || (manualUri.Scheme != Uri.UriSchemeHttp && manualUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("A valid http(s) PDF or issue-viewer URL is required.");
        }

        _downloadJobTrigger = DownloadJobTrigger.Manual;
        try
        {
            return await RunManualCoreAsync(
                newsSourceId,
                manualUri,
                saveAsDiscoveryPageUrl,
                enqueueOcr,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _downloadJobTrigger = null;
        }
    }

    private async Task<PdfEditionDownloadOutcome> RunManualCoreAsync(
        Guid newsSourceId,
        Uri manualUri,
        bool saveAsDiscoveryPageUrl,
        bool enqueueOcr,
        CancellationToken cancellationToken)
    {
        progress.Clear(newsSourceId);
        progress.Report(newsSourceId, 2, "Starting manual override…");

        var source = await LoadSourceAsync(newsSourceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("News source not found.");

        if (!source.IsEnabled)
        {
            throw new InvalidOperationException("This source is inactive. Enable it before running PDF download.");
        }

        if (!source.PdfDiscoveryEnabled)
        {
            throw new InvalidOperationException("PDF discovery is not enabled for this source.");
        }

        await audit.RecordAdminActionAsync(
            PdfEditionAuditEvents.ManualOverrideUsed,
            "NewsSource",
            source.Id.ToString(),
            new { source.Name, manualUrl = manualUri.ToString(), saveAsDiscoveryPageUrl },
            cancellationToken).ConfigureAwait(false);

        if (saveAsDiscoveryPageUrl && !manualUri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await WithDbContextAsync(async (db, ct) =>
            {
                var tracked = await db.NewsSources
                    .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct)
                    .ConfigureAwait(false);
                tracked.PdfDiscoveryPageUrl = manualUri.ToString();
                tracked.ModifiedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
            source.PdfDiscoveryPageUrl = manualUri.ToString();
        }

        progress.Report(newsSourceId, 12, "Resolving manual URL…");
        var candidate = await ResolveManualCandidateAsync(source, manualUri, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            const string failure = "Could not resolve the manual URL to a PDF edition. Paste a direct .pdf link or an issue viewer URL (e.g. /files/pdf/issue…/index.html).";
            return await PersistRowAsync(
                source.Id,
                null,
                PdfEditionStatus.Failed,
                failure,
                Array.Empty<PdfEditionCandidateDto>(),
                updateLastPdfUrl: false,
                source.PdfDiscoveryPageUrl ?? source.BaseUrl,
                cancellationToken).ConfigureAwait(false);
        }

        var candidateDtos = new List<PdfEditionCandidateDto>
        {
            new(candidate.Url.ToString(), candidate.Confidence, candidate.Method.ToString(), candidate.Label, candidate.IsTodayEdition)
        };

        progress.Report(newsSourceId, 28, "Validating PDF…");
        var (best, validation) = await ValidateBestCandidateAsync(
            new[] { candidate },
            source,
            ResolveWarmUpUrl(source),
            useHeadlessBrowser: source.UseHeadlessBrowser,
            cancellationToken).ConfigureAwait(false);

        if (best is null || validation is null || !validation.IsValid)
        {
            var failureReason = validation?.FailureReason
                ?? "The manual URL could not be validated as a PDF edition.";
            return await PersistRowAsync(
                source.Id,
                candidate,
                PdfEditionStatus.Failed,
                failureReason,
                candidateDtos,
                updateLastPdfUrl: false,
                candidate.Url.ToString(),
                cancellationToken).ConfigureAwait(false);
        }

        progress.Report(newsSourceId, 55, "PDF validated");
        return await DownloadValidatedCandidateAsync(
            source,
            best,
            candidateDtos,
            enqueueOcr,
            cancellationToken,
            validation).ConfigureAwait(false);
    }

    private async Task<PdfEditionDownloadOutcome> RunAsync(
        Guid newsSourceId,
        bool download,
        bool enqueueOcr,
        CancellationToken cancellationToken)
    {
        var ownedTriggerCapture = _downloadJobTrigger is null;
        _downloadJobTrigger ??= DownloadExecutionContext.CurrentTrigger;

        try
        {
            if (download)
            {
                progress.Clear(newsSourceId);
                progress.Report(newsSourceId, 2, "Starting…");
            }

            if (download)
            {
                progress.Report(newsSourceId, 8, "Loading source…");
            }

            var source = await LoadSourceAsync(newsSourceId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("News source not found.");

            if (!source.IsEnabled)
            {
                throw new InvalidOperationException("This source is inactive. Enable it before running PDF discovery or download.");
            }

            if (!source.PdfDiscoveryEnabled)
            {
                throw new InvalidOperationException("PDF discovery is not enabled for this source.");
            }

            if (download)
            {
                await WithDbContextAsync(async (db, ct) =>
                {
                    var tracked = await db.NewsSources
                        .FirstOrDefaultAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct)
                        .ConfigureAwait(false);
                    if (tracked is not null)
                    {
                        await AlAyamSourceBaselineGuard.EnsureKnownGoodConfigurationAsync(
                                db,
                                tracked,
                                logger,
                                ct)
                            .ConfigureAwait(false);
                    }

                    return true;
                }, cancellationToken).ConfigureAwait(false);

                source = await LoadSourceAsync(newsSourceId, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("News source not found.");
            }

            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DiscoveryStarted,
                "NewsSource",
                source.Id.ToString(),
                new { source.Name, download },
                cancellationToken).ConfigureAwait(false);

            if (download)
            {
                progress.Report(newsSourceId, 15, "Discovering PDF links…");
            }

            // Discover-only still needs Playwright for ManualSelector sources (e.g. Aawsat Download → Full Publication).
            var allowPlaywright = download || source.UseHeadlessBrowser;
            var discoveryResult = await discovery.DiscoverAsync(source, allowPlaywright, cancellationToken).ConfigureAwait(false);

        var candidateDtos = discoveryResult.Candidates
            .Select(c => new PdfEditionCandidateDto(
                c.Url.ToString(),
                c.Confidence,
                c.Method.ToString(),
                c.Label,
                c.IsTodayEdition))
            .ToList();

        foreach (var c in discoveryResult.Candidates.Take(10))
        {
            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.CandidateFound,
                "NewsSource",
                source.Id.ToString(),
                new { url = c.Url.ToString(), c.Confidence, method = c.Method.ToString() },
                cancellationToken).ConfigureAwait(false);
        }

        if (discoveryResult.Candidates.Count == 0)
        {
            var noPdfMessage = discoveryResult.DiscoveryFailureReason
                               ?? "No public PDF edition option was found for this source.";
            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DiscoveryNoPdfFound,
                "NewsSource",
                source.Id.ToString(),
                new { source.Name },
                cancellationToken).ConfigureAwait(false);

            return await PersistRowAsync(
                source.Id,
                null,
                PdfEditionStatus.NoPublicPdfAvailable,
                noPdfMessage,
                candidateDtos,
                updateLastPdfUrl: false,
                discoveryResult.PageUrl,
                cancellationToken).ConfigureAwait(false);
        }

        var warmUpUrl = ResolveWarmUpUrl(source);
        if (download)
        {
            progress.Report(newsSourceId, 32, "Validating PDF…");
        }

        var (best, validation) = await ValidateBestCandidateAsync(
            discoveryResult.Candidates,
            source,
            warmUpUrl,
            useHeadlessBrowser: allowPlaywright && source.UseHeadlessBrowser,
            cancellationToken).ConfigureAwait(false);

        if (best is null || validation is null || !validation.IsValid)
        {
            var failureReason = validation?.FailureReason ?? "No PDF candidate could be validated.";
            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DownloadFailed,
                "NewsSource",
                source.Id.ToString(),
                new { failureReason },
                cancellationToken).ConfigureAwait(false);

            return await PersistRowAsync(
                source.Id,
                discoveryResult.BestCandidate,
                PdfEditionStatus.Failed,
                failureReason,
                candidateDtos,
                updateLastPdfUrl: discoveryResult.BestCandidate is not null,
                discoveryResult.PageUrl,
                cancellationToken).ConfigureAwait(false);
        }

        if (download)
        {
            progress.Report(newsSourceId, 55, "PDF validated");
        }

        await audit.RecordAdminActionAsync(
            PdfEditionAuditEvents.CandidateValidated,
            "NewsSource",
            source.Id.ToString(),
            new { url = best.Url.ToString(), validation.ContentType, validation.SizeBytes },
            cancellationToken).ConfigureAwait(false);

        if (!download)
        {
            return await PersistRowAsync(
                source.Id,
                best,
                PdfEditionStatus.Validated,
                null,
                candidateDtos,
                updateLastPdfUrl: true,
                capturePageUrl: null,
                cancellationToken).ConfigureAwait(false);
        }

        return await DownloadValidatedCandidateAsync(
            source,
            best,
            candidateDtos,
            enqueueOcr,
            cancellationToken,
            validation).ConfigureAwait(false);
        }
        finally
        {
            if (ownedTriggerCapture)
            {
                _downloadJobTrigger = null;
            }
        }
    }

    private async Task<PdfEditionDownloadOutcome> DownloadValidatedCandidateAsync(
        NewsSource source,
        PdfEditionCandidate best,
        IReadOnlyList<PdfEditionCandidateDto> candidateDtos,
        bool enqueueOcr,
        CancellationToken cancellationToken,
        PdfEditionValidationResult? prevalidated = null)
    {
        var newsSourceId = source.Id;
        var warmUpUrl = ResolveWarmUpUrl(source);

        if (!source.IsDownloadAllowed)
        {
            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DownloadBlockedByCompliance,
                "NewsSource",
                source.Id.ToString(),
                new { url = best.Url.ToString(), reason = "IsDownloadAllowed=false" },
                cancellationToken).ConfigureAwait(false);

            return await PersistRowAsync(
                source.Id,
                best,
                PdfEditionStatus.BlockedByCompliance,
                "PDF discovery succeeded, but download is blocked because IsDownloadAllowed is false.",
                candidateDtos,
                updateLastPdfUrl: true,
                warmUpUrl?.ToString(),
                cancellationToken).ConfigureAwait(false);
        }

        progress.Report(newsSourceId, 62, "Checking compliance…");
        var complianceUri = Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var sourceUri)
            ? sourceUri
            : best.Url;
        var robots = await complianceGate.EvaluateAsync(complianceUri, source.AcquisitionMode, cancellationToken).ConfigureAwait(false);
        if (!robots.IsAllowed)
        {
            return await PersistRowAsync(
                source.Id,
                best,
                PdfEditionStatus.BlockedByCompliance,
                $"Download blocked by compliance: {robots.Detail}",
                candidateDtos,
                updateLastPdfUrl: true,
                warmUpUrl?.ToString(),
                cancellationToken).ConfigureAwait(false);
        }

        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await FindExistingDownloadAsync(source.Id, editionDate, cancellationToken).ConfigureAwait(false);

        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Sha256Hash))
        {
            await TouchLastPdfDownloadedFromRowAsync(source.Id, existing, cancellationToken).ConfigureAwait(false);
            await PersistRowAsync(
                source.Id,
                best,
                PdfEditionStatus.SkippedDuplicate,
                "Today's edition PDF already downloaded.",
                candidateDtos,
                updateLastPdfUrl: true,
                capturePageUrl: null,
                cancellationToken).ConfigureAwait(false);
            progress.Complete(newsSourceId);
            return MapOutcome(existing, candidateDtos);
        }

        await audit.RecordAdminActionAsync(
            PdfEditionAuditEvents.DownloadStarted,
            "NewsSource",
            source.Id.ToString(),
            new { url = best.Url.ToString() },
            cancellationToken).ConfigureAwait(false);

        progress.Report(newsSourceId, 68, "Downloading PDF…");

        try
        {
            byte[]? bytes = prevalidated?.ValidatedPdfBytes;
            if (bytes is null || bytes.Length == 0)
            {
                if (best.Method == PdfDiscoveryMethod.ManualOverride
                    && best.Label?.Contains("Full Publication", StringComparison.OrdinalIgnoreCase) == true
                    && AawsatFullPublicationPdf.UsesClickPath(source))
                {
                    bytes = await AawsatFullPublicationPdf.TryDownloadBytesAsync(
                        best.Url,
                        source,
                        logger,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (best.Method == PdfDiscoveryMethod.ConfiguredDownloadSelector
                         && best.Label?.Contains("Full Publication", StringComparison.OrdinalIgnoreCase) == true
                         && AawsatFullPublicationPdf.UsesClickPath(source))
                {
                    bytes = await AawsatFullPublicationPdf.TryDownloadBytesWithFallbacksAsync(
                        best.Url,
                        source,
                        ResolveWarmUpUrl(source),
                        logger,
                        cancellationToken).ConfigureAwait(false);
                }
                else if ((best.Method == PdfDiscoveryMethod.ConfiguredLinkSelector
                          || AlAyamFullEditionPdf.IsDirectPdfUrl(best.Url))
                         && AlAyamFullEditionPdf.UsesClickPath(source))
                {
                    bytes = await AlAyamFullEditionPdf.TryDownloadBytesWithFallbacksAsync(
                        best.Url,
                        source,
                        ResolveWarmUpUrl(source),
                        httpClientFactory,
                        logger,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    bytes = await contentFetcher.FetchAsync(
                        best.Url,
                        source.UseHeadlessBrowser,
                        warmUpUrl,
                        source,
                        cancellationToken,
                        bytePct => progress.Report(newsSourceId, 68 + bytePct * 22 / 100, "Downloading PDF…")).ConfigureAwait(false);
                }
            }
            else
            {
                progress.Report(newsSourceId, 90, "Downloading PDF…");
            }

            if (bytes is null || bytes.Length == 0)
            {
                throw new InvalidOperationException("PDF download returned empty content.");
            }

            if (!PdfEditionContentFetcher.IsPdf(bytes))
            {
                throw new InvalidOperationException("Downloaded content is not a valid PDF.");
            }

            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            if (existing is not null && existing.Sha256Hash == hash)
            {
                await TouchLastPdfDownloadedFromRowAsync(source.Id, existing, cancellationToken).ConfigureAwait(false);
                await PersistRowAsync(
                    source.Id,
                    best,
                    PdfEditionStatus.SkippedDuplicate,
                    "Duplicate file hash for today.",
                    candidateDtos,
                    updateLastPdfUrl: true,
                    capturePageUrl: null,
                    cancellationToken).ConfigureAwait(false);
                progress.Complete(newsSourceId);
                return MapOutcome(existing, candidateDtos);
            }

            var relativePath = BuildStoragePath(source.Name, editionDate);
            progress.Report(newsSourceId, 92, "Saving file…");
            var stored = await fileStorage.WriteAsync(relativePath, bytes, cancellationToken).ConfigureAwait(false);

            progress.Report(newsSourceId, 96, "Finalizing…");
            var outcome = await PersistDownloadAsync(
                source.Id,
                best,
                candidateDtos,
                prevalidated?.ContentType ?? "application/pdf",
                stored.RelativeKey,
                stored.SizeBytes,
                hash,
                editionDate,
                cancellationToken).ConfigureAwait(false);

            if (enqueueOcr && outcome.DownloadJobId is Guid jobId)
            {
                intelligenceScheduler.EnqueueProcessDownloadJob(jobId);
            }

            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DownloadCompleted,
                "NewsSource",
                source.Id.ToString(),
                new { path = stored.RelativeKey, hash, jobId = outcome.DownloadJobId, fileId = outcome.DownloadedFileId },
                cancellationToken).ConfigureAwait(false);

            progress.Complete(newsSourceId);
            return outcome;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF download failed for {Source}", source.Name);
            await audit.RecordAdminActionAsync(
                PdfEditionAuditEvents.DownloadFailed,
                "NewsSource",
                source.Id.ToString(),
                new { url = best.Url.ToString(), error = ex.Message },
                cancellationToken).ConfigureAwait(false);

            return await PersistRowAsync(
                source.Id,
                best,
                PdfEditionStatus.Failed,
                ex.Message,
                candidateDtos,
                updateLastPdfUrl: true,
                best.Url.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PdfEditionCandidate?> ResolveManualCandidateAsync(
        NewsSource source,
        Uri manualUri,
        CancellationToken cancellationToken)
    {
        if (manualUri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new PdfEditionCandidate(manualUri, 1.0, PdfDiscoveryMethod.ManualOverride, "Manual PDF URL", true);
        }

        if (manualUri.AbsolutePath.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase)
            && AawsatFullPublicationPdf.UsesClickPath(source))
        {
            var aawsat = await AawsatFullPublicationPdf.TryDiscoverAsync(manualUri, source, logger, cancellationToken)
                .ConfigureAwait(false);
            if (aawsat is not null)
            {
                return new PdfEditionCandidate(
                    aawsat.PdfUrl,
                    0.99,
                    PdfDiscoveryMethod.ManualOverride,
                    "Full Publication (manual issue URL)",
                    true);
            }
        }

        if (AlAyamFullEditionPdf.UsesClickPath(source)
            && (AlAyamFullEditionPdf.IsDirectPdfUrl(manualUri)
                || manualUri.AbsolutePath.Contains("/epaper", StringComparison.OrdinalIgnoreCase)))
        {
            var alAyam = await AlAyamFullEditionPdf.TryDiscoverAsync(manualUri, source, httpClientFactory, logger, cancellationToken)
                .ConfigureAwait(false);
            if (alAyam?.PdfUrl is not null)
            {
                return new PdfEditionCandidate(
                    alAyam.PdfUrl,
                    0.99,
                    PdfDiscoveryMethod.ManualOverride,
                    "كل الصفحات (manual)",
                    true);
            }
        }

        if (source.UseHeadlessBrowser)
        {
            var bytes = await AawsatFullPublicationPdf.TryDownloadBytesAsync(manualUri, source, logger, cancellationToken)
                .ConfigureAwait(false);
            if (bytes is not null && bytes.Length > 0)
            {
                return new PdfEditionCandidate(
                    manualUri,
                    0.95,
                    PdfDiscoveryMethod.ManualOverride,
                    "Full Publication (manual page URL)",
                    true);
            }
        }

        return new PdfEditionCandidate(manualUri, 0.7, PdfDiscoveryMethod.ManualOverride, "Manual URL", false);
    }

    private Task<NewsSource?> LoadSourceAsync(Guid newsSourceId, CancellationToken cancellationToken) =>
        WithDbContextAsync(
            (db, ct) => db.NewsSources.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct),
            cancellationToken);

    private Task<PdfEditionDownload?> FindExistingDownloadAsync(
        Guid newsSourceId,
        DateOnly editionDate,
        CancellationToken cancellationToken) =>
        WithDbContextAsync(
            (db, ct) => db.PdfEditionDownloads.AsNoTracking()
                .Where(x => !x.IsDeleted && x.NewsSourceId == newsSourceId && x.EditionDate == editionDate && x.Status == PdfEditionStatus.Downloaded)
                .OrderByDescending(x => x.DownloadedAt)
                .FirstOrDefaultAsync(ct),
            cancellationToken);

    private Task TouchLastPdfDownloadedFromRowAsync(
        Guid newsSourceId,
        PdfEditionDownload existing,
        CancellationToken cancellationToken) =>
        WithDbContextAsync(async (db, ct) =>
        {
            if (existing.DownloadedAt is null)
            {
                return false;
            }

            var source = await db.NewsSources
                .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct)
                .ConfigureAwait(false);

            source.LastPdfDownloadedAt = existing.DownloadedAt;
            source.LastDownloadAt = existing.DownloadedAt;
            if (!string.IsNullOrWhiteSpace(existing.SavedPath))
            {
                source.LastSavedPdfPath = existing.SavedPath;
            }

            if (!string.IsNullOrWhiteSpace(existing.SourceUrl))
            {
                source.LastPdfUrl = existing.SourceUrl;
            }

            source.LastPdfDiscoveryOutcome = SourcePdfDiscoveryOutcome.RealPdfFound;
            source.ModifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private async Task<PdfEditionDownloadOutcome> PersistRowAsync(
        Guid newsSourceId,
        PdfEditionCandidate? candidate,
        PdfEditionStatus status,
        string? failureReason,
        IReadOnlyList<PdfEditionCandidateDto> candidateDtos,
        bool updateLastPdfUrl,
        string? capturePageUrl,
        CancellationToken cancellationToken)
    {
        Guid? failureJobId = null;
        var outcome = await WithDbContextAsync(async (db, ct) =>
        {
            var source = await db.NewsSources
                .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct)
                .ConfigureAwait(false);

            source.LastPdfDiscoveredAt = DateTimeOffset.UtcNow;
            if (updateLastPdfUrl && candidate is not null)
            {
                source.LastPdfUrl = candidate.Url.ToString();
            }

            source.LastPdfDiscoveryOutcome = status switch
            {
                PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.Discovered =>
                    SourcePdfDiscoveryOutcome.RealPdfFound,
                PdfEditionStatus.NoPublicPdfAvailable => SourcePdfDiscoveryOutcome.NoPublicPdfAvailable,
                _ => source.LastPdfDiscoveryOutcome
            };

            var row = CreateRow(source, candidate, status, failureReason);
            if (_boundDownloadJobId is Guid boundJobId
                && status is PdfEditionStatus.SkippedDuplicate or PdfEditionStatus.BlockedByCompliance)
            {
                var boundJob = await db.DownloadJobs
                    .FirstAsync(j => j.Id == boundJobId && !j.IsDeleted, ct)
                    .ConfigureAwait(false);
                boundJob.Status = status == PdfEditionStatus.SkippedDuplicate
                    ? DownloadJobStatus.Succeeded
                    : DownloadJobStatus.Failed;
                boundJob.ErrorMessage = status == PdfEditionStatus.SkippedDuplicate ? null : failureReason;
                boundJob.StartedAt ??= DateTimeOffset.UtcNow;
                boundJob.CompletedAt = DateTimeOffset.UtcNow;
                row.DownloadJobId = boundJob.Id;
            }
            else if (status is PdfEditionStatus.Failed or PdfEditionStatus.NoPublicPdfAvailable)
            {
                DownloadJob failureJob;
                if (_boundDownloadJobId is Guid boundId)
                {
                    failureJob = await db.DownloadJobs
                        .FirstAsync(j => j.Id == boundId && !j.IsDeleted, ct)
                        .ConfigureAwait(false);
                    failureJob.Status = DownloadJobStatus.Failed;
                    failureJob.ErrorMessage = failureReason ?? "PDF edition download failed.";
                    failureJob.StartedAt ??= DateTimeOffset.UtcNow;
                    failureJob.CompletedAt = DateTimeOffset.UtcNow;
                    failureJob.Trigger = EffectiveDownloadJobTrigger;
                }
                else
                {
                    failureJob = new DownloadJob
                    {
                        Id = Guid.NewGuid(),
                        NewsSourceId = source.Id,
                        Status = DownloadJobStatus.Failed,
                        ErrorMessage = failureReason ?? "PDF edition download failed.",
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow,
                        CorrelationId = Guid.NewGuid().ToString("N"),
                        Trigger = EffectiveDownloadJobTrigger,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RobotsTxtAllowed = true
                    };
                    db.DownloadJobs.Add(failureJob);
                }

                row.DownloadJobId = failureJob.Id;
                failureJobId = failureJob.Id;
            }

            db.PdfEditionDownloads.Add(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return MapOutcome(row, candidateDtos);
        }, cancellationToken).ConfigureAwait(false);

        if (failureJobId is Guid jobId)
        {
            await failureArtifacts.EnsureFailureArtifactsAsync(
                newsSourceId,
                jobId,
                capturePageUrl ?? candidate?.Url.ToString(),
                failureReason ?? "PDF edition download failed.",
                failureCode: null,
                cancellationToken).ConfigureAwait(false);

            using var scope = scopeFactory.CreateScope();
            var enqueue = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryEnqueueService>();
            var failedJob = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().DownloadJobs
                .AsNoTracking()
                .FirstAsync(j => j.Id == jobId, cancellationToken)
                .ConfigureAwait(false);
            await enqueue.TryEnqueueAfterFailureAsync(failedJob, cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    private Task<PdfEditionDownloadOutcome> PersistDownloadAsync(
        Guid newsSourceId,
        PdfEditionCandidate candidate,
        IReadOnlyList<PdfEditionCandidateDto> candidateDtos,
        string? contentType,
        string relativePath,
        long sizeBytes,
        string hash,
        DateOnly editionDate,
        CancellationToken cancellationToken) =>
        WithDbContextAsync(async (db, ct) =>
        {
            var source = await db.NewsSources
                .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, ct)
                .ConfigureAwait(false);

            DownloadJob job;
            if (_boundDownloadJobId is Guid boundId)
            {
                job = await db.DownloadJobs
                    .FirstAsync(j => j.Id == boundId && !j.IsDeleted, ct)
                    .ConfigureAwait(false);
                job.Status = DownloadJobStatus.Succeeded;
                job.ErrorMessage = null;
                job.StartedAt ??= DateTimeOffset.UtcNow;
                job.CompletedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                job = new DownloadJob
                {
                    Id = Guid.NewGuid(),
                    NewsSourceId = source.Id,
                    Status = DownloadJobStatus.Succeeded,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Trigger = EffectiveDownloadJobTrigger,
                    CreatedAt = DateTimeOffset.UtcNow,
                    RobotsTxtAllowed = true
                };
                db.DownloadJobs.Add(job);
            }

            var downloadedFile = new DownloadedFile
            {
                Id = Guid.NewGuid(),
                DownloadJobId = job.Id,
                ContentType = contentType ?? "application/pdf",
                OriginalUrl = candidate.Url.ToString(),
                BlobUri = relativePath,
                SizeBytes = sizeBytes,
                Sha256 = hash,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.DownloadedFiles.Add(downloadedFile);

            var row = CreateRow(source, candidate, PdfEditionStatus.Downloaded, null);
            row.DownloadJobId = job.Id;
            row.DownloadedFileId = downloadedFile.Id;
            row.SavedPath = relativePath;
            row.FileSizeBytes = sizeBytes;
            row.Sha256Hash = hash;
            row.DownloadedAt = DateTimeOffset.UtcNow;
            row.EditionDate = editionDate;
            db.PdfEditionDownloads.Add(row);

            source.LastPdfDiscoveredAt = DateTimeOffset.UtcNow;
            source.LastPdfUrl = candidate.Url.ToString();
            source.LastPdfDownloadedAt = row.DownloadedAt;
            source.LastSavedPdfPath = relativePath;
            source.LastDownloadAt = row.DownloadedAt;
            source.LastPdfDiscoveryOutcome = SourcePdfDiscoveryOutcome.RealPdfFound;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return MapOutcome(row, candidateDtos);
        }, cancellationToken);

    private static PdfEditionDownload CreateRow(
        NewsSource source,
        PdfEditionCandidate? candidate,
        PdfEditionStatus status,
        string? failureReason) =>
        new()
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            SourceUrl = candidate?.Url.ToString()
                ?? (status == PdfEditionStatus.NoPublicPdfAvailable
                    ? source.PdfDiscoveryPageUrl ?? source.BaseUrl ?? string.Empty
                    : source.LastPdfUrl ?? string.Empty),
            EditionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DiscoveryConfidence = candidate?.Confidence ?? 0,
            DiscoveryMethod = candidate?.Method ?? PdfDiscoveryMethod.Unknown,
            Status = status,
            FailureReason = failureReason,
            DiscoveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private Task FailBoundDownloadJobAsync(Guid downloadJobId, string errorMessage, CancellationToken cancellationToken) =>
        WithDbContextAsync(async (db, ct) =>
        {
            var job = await db.DownloadJobs
                .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, ct)
                .ConfigureAwait(false);
            if (job is null || job.Status is DownloadJobStatus.Succeeded or DownloadJobStatus.Failed)
            {
                return false;
            }

            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = errorMessage;
            job.StartedAt ??= DateTimeOffset.UtcNow;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private async Task<T> WithDbContextAsync<T>(
        Func<IApplicationDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await action(db, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(PdfEditionCandidate? Candidate, PdfEditionValidationResult? Validation)> ValidateBestCandidateAsync(
        IReadOnlyList<PdfEditionCandidate> candidates,
        NewsSource source,
        Uri? warmUpUrl,
        bool useHeadlessBrowser,
        CancellationToken cancellationToken)
    {
        PdfEditionValidationResult? lastResult = null;
        foreach (var candidate in candidates
                     .GroupBy(c => c.Url.ToString(), StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.OrderByDescending(x => x.Confidence).First())
                     .OrderByDescending(c => c.IsTodayEdition && source.PreferTodayEdition)
                     .ThenByDescending(c => c.Confidence)
                     .Take(8))
        {
            if (AawsatFullPublicationPdf.UsesClickPath(source)
                && (candidate.Method == PdfDiscoveryMethod.ConfiguredDownloadSelector
                    || AawsatFullPublicationPdf.IsIssueViewerUrl(candidate.Url)))
            {
                var clickBytes = await AawsatFullPublicationPdf.TryDownloadBytesWithFallbacksAsync(
                    candidate.Url,
                    source,
                    warmUpUrl,
                    logger,
                    cancellationToken).ConfigureAwait(false);
                if (clickBytes is not null && clickBytes.Length > 0)
                {
                    if (PdfEditionContentFetcher.IsPdf(clickBytes))
                    {
                        if (clickBytes.Length >= source.MinimumPdfSizeKb * 1024L)
                        {
                            return (candidate, new PdfEditionValidationResult(true, "application/pdf", clickBytes.Length, null, clickBytes));
                        }

                        lastResult = new PdfEditionValidationResult(
                            false,
                            "application/pdf",
                            clickBytes.Length,
                            $"File smaller than minimum {source.MinimumPdfSizeKb} KB.");
                        continue;
                    }

                    lastResult = new PdfEditionValidationResult(
                        false,
                        null,
                        clickBytes.Length,
                        "Asharq Al-Awsat Full Publication click path returned non-PDF content.");
                    continue;
                }

                lastResult = new PdfEditionValidationResult(
                    false,
                    null,
                    null,
                    "Asharq Al-Awsat Full Publication click path could not download a PDF. Issue viewer HTML is not a valid PDF.");
                continue;
            }

            if (AlAyamFullEditionPdf.UsesClickPath(source)
                && (candidate.Method == PdfDiscoveryMethod.ConfiguredLinkSelector
                    || AlAyamFullEditionPdf.IsDirectPdfUrl(candidate.Url)))
            {
                var clickBytes = await AlAyamFullEditionPdf.TryDownloadBytesWithFallbacksAsync(
                    candidate.Url,
                    source,
                    warmUpUrl,
                    httpClientFactory,
                    logger,
                    cancellationToken).ConfigureAwait(false);
                if (clickBytes is not null && clickBytes.Length > 0)
                {
                    if (PdfEditionContentFetcher.IsPdf(clickBytes))
                    {
                        if (clickBytes.Length >= source.MinimumPdfSizeKb * 1024L)
                        {
                            return (candidate, new PdfEditionValidationResult(true, "application/pdf", clickBytes.Length, null, clickBytes));
                        }

                        lastResult = new PdfEditionValidationResult(
                            false,
                            "application/pdf",
                            clickBytes.Length,
                            $"File smaller than minimum {source.MinimumPdfSizeKb} KB.");
                        continue;
                    }

                    lastResult = new PdfEditionValidationResult(
                        false,
                        null,
                        clickBytes.Length,
                        "Al Ayam all-pages click path returned non-PDF content.");
                    continue;
                }

                lastResult = new PdfEditionValidationResult(
                    false,
                    null,
                    null,
                    "Al Ayam all-pages click path could not download a PDF from i.alayam.com.");
                continue;
            }

            var result = await validator.ValidateAsync(
                candidate.Url,
                source.RequirePdfContentType,
                source.MinimumPdfSizeKb,
                useHeadlessBrowser,
                warmUpUrl,
                cancellationToken,
                source).ConfigureAwait(false);

            if (result.IsValid)
            {
                return (candidate, result);
            }

            lastResult = result;
            logger.LogInformation(
                "PDF candidate rejected for {Source}: {Url} — {Reason}",
                source.Name,
                candidate.Url,
                result.FailureReason);
        }

        return (null, lastResult);
    }

    private static Uri? ResolveWarmUpUrl(NewsSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.PdfDiscoveryPageUrl) && Uri.TryCreate(source.PdfDiscoveryPageUrl, UriKind.Absolute, out var pageUrl))
        {
            return pageUrl;
        }

        if (Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            return baseUrl;
        }

        return null;
    }

    private string BuildStoragePath(string sourceName, DateOnly editionDate)
    {
        var safe = Regex.Replace(sourceName, @"[^\w\s\-]", "").Trim().Replace(' ', '-');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "source";
        }

        return $"{_storage.NewspapersRelativePath.TrimEnd('/')}/{safe}/{editionDate:yyyy-MM-dd}/today-edition.pdf"
            .Replace('\\', '/');
    }

    private static PdfEditionDownloadOutcome MapOutcome(PdfEditionDownload row, IReadOnlyList<PdfEditionCandidateDto>? candidates) =>
        new(
            row.Id,
            row.DownloadJobId,
            row.DownloadedFileId,
            row.Status,
            row.SourceUrl,
            row.SavedPath,
            row.DownloadedFileId is Guid fid ? $"/api/v1/news-sources/{row.NewsSourceId}/pdf/{fid}" : null,
            row.FileSizeBytes,
            row.Sha256Hash,
            row.DiscoveryConfidence,
            row.DiscoveryMethod.ToString(),
            row.FailureReason,
            candidates ?? Array.Empty<PdfEditionCandidateDto>());
}
