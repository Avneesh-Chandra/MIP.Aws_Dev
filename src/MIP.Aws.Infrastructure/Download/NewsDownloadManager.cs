using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.ServiceModel.Syndication;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Articles;
using MIP.Aws.Application.Abstractions.Browser;
using MIP.Aws.Application.Abstractions.Crawling;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Jobs;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Abstractions.Portal;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Connectors;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Infrastructure.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MIP.Aws.Infrastructure.Download;

public sealed class NewsDownloadManager(
    IApplicationDbContext db,
    INewsSourceConnectorFactory connectors,
    IContentDownloader downloader,
    IHeadlessBrowserService headless,
    IRobotsPolicyService robots,
    IFileStorageService fileStorage,
    INewsCredentialProtector credentialProtector,
    IWebPortalAutomationService portalAutomation,
    IPdfEditionDownloadService pdfEditionDownloads,
    ISourceRecoveryOrchestrator recoveryOrchestrator,
    IAutoAiDownloadRecoveryEnqueueService autoAiRecoveryEnqueue,
    IEnumerable<IArticleExtractor> extractors,
    HtmlEditionStoryExtractor editionStoryExtractor,
    IIntelligenceJobScheduler intelligenceJobs,
    ILogger<NewsDownloadManager> logger,
    IOptions<StorageOptions> storageOptions) : IDownloadManager
{
    private readonly StorageOptions _storage = storageOptions.Value;

    public async Task ExecuteSourceDownloadAsync(Guid newsSourceId, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == newsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
        {
            logger.LogWarning("News source {SourceId} not found.", newsSourceId);
            return;
        }

        if (!source.IsEnabled)
        {
            logger.LogInformation("Skipping disabled source {SourceId}", newsSourceId);
            return;
        }

        var previousFailures = await db.DownloadJobs.AsNoTracking()
            .CountAsync(j => j.NewsSourceId == source.Id && j.Status == DownloadJobStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            Status = DownloadJobStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Trigger = DownloadExecutionContext.CurrentTrigger,
            CreatedAt = DateTimeOffset.UtcNow,
            RetryCount = previousFailures,
            RobotsTxtAllowed = true
        };

        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteDownloadJobAsync(job.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteDownloadJobAsync(Guid downloadJobId, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var job = await db.DownloadJobs
            .Include(j => j.NewsSource)!.ThenInclude(s => s!.Credential)
            .Include(j => j.NewsSource)!.ThenInclude(s => s!.DownloadSchedule)
            .FirstOrDefaultAsync(j => j.Id == downloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (job?.NewsSource is not NewsSource source)
        {
            logger.LogWarning("Download job {JobId} was not found.", downloadJobId);
            return;
        }

        if (job.Status is DownloadJobStatus.Succeeded or DownloadJobStatus.Failed)
        {
            await TryFinalizeRecoveryAttemptAsync(job, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!source.IsEnabled)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = "Source is disabled.";
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        job.Status = DownloadJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        job.CompletedAt = null;
        job.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var authHeaders = BuildAuthHeaders(source);
        int? lastHttp = null;
        var robotsOk = true;
        var anyDownloaded = false;
        var portalHandled = false;

        try
        {
            if (source.SourceType == NewsSourceType.ManualUpload)
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage = "ManualUpload sources do not support automated download. Use the manual artifact workflow.";
                return;
            }

            if (source.SourceType == NewsSourceType.WebPortalLogin)
            {
                await portalAutomation.RunLicensedPortalDownloadForJobAsync(job.Id, cancellationToken).ConfigureAwait(false);
                await RefreshJobFromStoreAsync(job, cancellationToken).ConfigureAwait(false);
                portalHandled = true;
                if (job.Status == DownloadJobStatus.Succeeded)
                {
                    source.LastDownloadAt = DateTimeOffset.UtcNow;
                }

                return;
            }

            if (source.PdfDiscoveryEnabled
                && source.SourceType is NewsSourceType.PublicPdf or NewsSourceType.PublicHtml)
            {
                await pdfEditionDownloads.ExecuteDownloadJobAsync(job.Id, cancellationToken).ConfigureAwait(false);
                await RefreshJobFromStoreAsync(job, cancellationToken).ConfigureAwait(false);
                portalHandled = true;
                if (job.Status == DownloadJobStatus.Succeeded)
                {
                    source.LastDownloadAt = DateTimeOffset.UtcNow;
                }

                return;
            }

            var connector = connectors.Resolve(source);
            var plan = await connector.BuildDownloadPlanAsync(source, cancellationToken).ConfigureAwait(false);
            if (plan.Count == 0)
            {
                throw new InvalidOperationException("Connector produced an empty download plan.");
            }

            foreach (var candidate in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var allowed = await robots.IsAllowedAsync(candidate.ResourceUri, source.AcquisitionMode, cancellationToken).ConfigureAwait(false);
                if (!allowed)
                {
                    robotsOk = false;
                    logger.LogWarning("Robots/compliance denied {Url} for source {SourceId}", candidate.ResourceUri, source.Id);
                    continue;
                }

                DownloadedContent downloaded;
                if (source.UseHeadlessBrowser && source.SourceType == NewsSourceType.PublicHtml)
                {
                    var html = await headless.GetRenderedHtmlAsync(candidate.ResourceUri, TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(html))
                    {
                        var bytes = Encoding.UTF8.GetBytes(html);
                        downloaded = new DownloadedContent(candidate.ResourceUri, bytes, "text/html", null, 200);
                    }
                    else
                    {
                        downloaded = await downloader.DownloadAsync(candidate.ResourceUri, authHeaders, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    downloaded = await downloader.DownloadAsync(candidate.ResourceUri, authHeaders, cancellationToken).ConfigureAwait(false);
                }

                lastHttp = downloaded.HttpStatusCode;
                anyDownloaded = true;

                var ext = PickExtension(downloaded.ContentType, candidate.ResourceUri);
                var relative = Path.Combine(_storage.RawRelativePath, source.Id.ToString("N"), job.Id.ToString("N"), $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}")
                    .Replace(Path.DirectorySeparatorChar, '/');

                var stored = await fileStorage.WriteAsync(relative, downloaded.Payload, cancellationToken).ConfigureAwait(false);
                var sha = Convert.ToHexString(SHA256.HashData(downloaded.Payload));

                var fileEntity = new DownloadedFile
                {
                    Id = Guid.NewGuid(),
                    DownloadJobId = job.Id,
                    ContentType = downloaded.ContentType,
                    OriginalUrl = candidate.ResourceUri.ToString(),
                    BlobUri = stored.RelativeKey,
                    SizeBytes = stored.SizeBytes,
                    Sha256 = sha,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.DownloadedFiles.Add(fileEntity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await PersistArticlesAsync(source, fileEntity, downloaded, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!anyDownloaded)
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage ??= "No files were downloaded (all URLs may have been blocked).";
            }
            else
            {
                job.Status = robotsOk ? DownloadJobStatus.Succeeded : DownloadJobStatus.Failed;
                if (!robotsOk)
                {
                    job.ErrorMessage ??= "One or more URLs were blocked by robots/compliance policy.";
                }

                source.LastDownloadAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            logger.LogError(ex, "Download failed for source {SourceId}", source.Id);
        }
        finally
        {
            sw.Stop();
            if (portalHandled)
            {
                await RefreshJobFromStoreAsync(job, cancellationToken).ConfigureAwait(false);
            }

            if (job.Status is DownloadJobStatus.Running or DownloadJobStatus.Pending)
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage ??= "Download did not complete.";
            }

            if (job.CompletedAt is null)
            {
                job.CompletedAt = DateTimeOffset.UtcNow;
            }

            if (!portalHandled || job.DurationMs is null)
            {
                job.DurationMs = sw.ElapsedMilliseconds;
            }

            if (!portalHandled)
            {
                job.HttpStatusCode = lastHttp;
                job.RobotsTxtAllowed = robotsOk;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await TryFinalizeRecoveryAttemptAsync(job, cancellationToken).ConfigureAwait(false);

            if (job.Status == DownloadJobStatus.Failed)
            {
                await autoAiRecoveryEnqueue.TryEnqueueAfterFailureAsync(job, cancellationToken).ConfigureAwait(false);
            }

            if (job.Status == DownloadJobStatus.Succeeded)
            {
                intelligenceJobs.EnqueueProcessDownloadJob(job.Id);
            }
        }
    }

    public Task<int> CleanupOldArtifactsAsync(int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        return fileStorage.DeleteOlderThanAsync(cutoff, cancellationToken);
    }

    public async Task<int> RetryFailedJobsAsync(CancellationToken cancellationToken)
    {
        var failedSourceIds = await (
                from j in db.DownloadJobs.AsNoTracking()
                join s in db.NewsSources.AsNoTracking() on j.NewsSourceId equals s.Id
                where j.Status == DownloadJobStatus.Failed
                      && j.RetryCount < 3
                      && s.IsEnabled
                      && !s.IsDeleted
                select j.NewsSourceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (failedSourceIds.Count == 0)
        {
            return 0;
        }

        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => failedSourceIds.Contains(s.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRetry = sources
            .Where(s => !PdfManagementSourceRules.IsPdfDownloadMonitoredSource(s))
            .Select(s => s.Id)
            .ToList();

        foreach (var id in toRetry)
        {
            await ExecuteSourceDownloadAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return toRetry.Count;
    }

    private async Task RefreshJobFromStoreAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        if (db is DbContext context)
        {
            await context.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var latest = await db.DownloadJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == job.Id, cancellationToken)
            .ConfigureAwait(false);
        if (latest is null)
        {
            return;
        }

        job.Status = latest.Status;
        job.ErrorMessage = latest.ErrorMessage;
        job.CompletedAt = latest.CompletedAt;
        job.DurationMs = latest.DurationMs;
        job.HttpStatusCode = latest.HttpStatusCode;
        job.RobotsTxtAllowed = latest.RobotsTxtAllowed;
        job.StartedAt = latest.StartedAt;
    }

    private async Task PersistArticlesAsync(NewsSource source, DownloadedFile file, DownloadedContent downloaded, CancellationToken cancellationToken)
    {
        if (downloaded.ContentType.Contains("rss", StringComparison.OrdinalIgnoreCase) ||
            downloaded.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
            source.SourceType == NewsSourceType.Rss)
        {
            try
            {
                using var ms = new MemoryStream(downloaded.Payload);
                using var reader = XmlReader.Create(ms, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, Async = true });
                var feed = SyndicationFeed.Load(reader);
                foreach (var item in feed.Items.Take(200))
                {
                    var link = item.Links.FirstOrDefault(l => string.Equals(l.RelationshipType, "alternate", StringComparison.OrdinalIgnoreCase))?.Uri
                        ?? item.Links.FirstOrDefault()?.Uri;
                    var headline = item.Title?.Text?.Trim() ?? "(untitled)";
                    var summary = item.Summary?.Text ?? string.Empty;
                    var published = item.PublishDate == default ? item.LastUpdatedTime : item.PublishDate;
                    var canon = link?.ToString();
                    await AddArticleIfNewAsync(source, file, headline, summary, summary, headline, item.Authors.FirstOrDefault()?.Name, published, feed.Title?.Text, Array.Empty<string>(), canon, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RSS syndication parse failed for {Url}", downloaded.SourceUri);
            }

            return;
        }

        if (source.SourceType == NewsSourceType.PublicPdf || downloaded.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (downloaded.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
            source.SourceType == NewsSourceType.PublicHtml)
        {
            var html = Encoding.UTF8.GetString(downloaded.Payload);
            var editionStories = editionStoryExtractor.Extract(source, html, 15)
                .Where(s => HeadlineQuality.IsReadableHeadline(s.Headline, source.Name))
                .ToList();

            if (editionStories.Count >= 2)
            {
                foreach (var story in editionStories)
                {
                    var snippet = story.Snippet ?? story.Headline;
                    await AddArticleIfNewAsync(
                        source,
                        file,
                        story.Headline,
                        snippet,
                        html,
                        story.Headline,
                        null,
                        DateTimeOffset.UtcNow,
                        null,
                        Array.Empty<string>(),
                        story.Url,
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }
        }

        var extractor = extractors.FirstOrDefault(e => e.Supports(downloaded.ContentType, downloaded.SourceUri));
        if (extractor is null)
        {
            return;
        }

        var result = await extractor.ExtractAsync(downloaded.SourceUri, downloaded.Payload, downloaded.ContentType, source.DefaultLanguage, cancellationToken).ConfigureAwait(false);
        await AddArticleIfNewAsync(
            source,
            file,
            result.Headline,
            result.CleanText,
            result.RawHtml,
            result.Headline,
            result.Author,
            result.PublishedAt,
            result.Section,
            result.Tags,
            result.CanonicalUrl ?? downloaded.SourceUri.ToString(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task AddArticleIfNewAsync(
        NewsSource source,
        DownloadedFile file,
        string headline,
        string cleanText,
        string rawHtml,
        string fingerprintHeadline,
        string? author,
        DateTimeOffset? published,
        string? section,
        IReadOnlyList<string> tags,
        string? canonicalUrl,
        CancellationToken cancellationToken)
    {
        var fp = BuildFingerprint(canonicalUrl ?? file.OriginalUrl, fingerprintHeadline);
        var exists = await db.ExtractedArticles.AsNoTracking().AnyAsync(a => a.ContentFingerprint == fp && !a.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        db.ExtractedArticles.Add(new ExtractedArticle
        {
            Id = Guid.NewGuid(),
            DownloadedFileId = file.Id,
            Headline = headline[..Math.Min(headline.Length, 500)],
            RawContent = rawHtml.Length > 500_000 ? rawHtml[..500_000] : rawHtml,
            CleanedContent = cleanText.Length > 500_000 ? cleanText[..500_000] : cleanText,
            Author = author,
            PublishedAt = published,
            Section = section,
            Language = source.DefaultLanguage ?? "und",
            TagsJson = tags.Count > 0 ? JsonSerializer.Serialize(tags) : null,
            CanonicalUrl = canonicalUrl,
            ContentFingerprint = fp,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string BuildFingerprint(string canonical, string headline)
    {
        var norm = $"{canonical}\n{headline.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm)));
    }

    private static string PickExtension(string contentType, Uri uri)
    {
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ".pdf";
        }

        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) || contentType.Contains("rss", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml";
        }

        return ".html";
    }

    private IReadOnlyDictionary<string, string>? BuildAuthHeaders(NewsSource source)
    {
        if (!source.RequiresAuthentication || source.Credential?.ProtectedCredentialPayload is null)
        {
            return null;
        }

        var cred = credentialProtector.Unprotect(source.Credential.ProtectedCredentialPayload);
        if (cred is null)
        {
            return null;
        }

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cred.Value.Username}:{cred.Value.Password}"));
        return new Dictionary<string, string> { { "Authorization", $"Basic {token}" } };
    }

    private async Task TryFinalizeRecoveryAttemptAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.CorrelationId)
            || !job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Guid.TryParse(job.CorrelationId["recovery:".Length..], out var attemptId))
        {
            return;
        }

        try
        {
            await recoveryOrchestrator.FinalizeAttemptAsync(attemptId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not finalize recovery attempt {AttemptId} after job {JobId}", attemptId, job.Id);
        }
    }
}
