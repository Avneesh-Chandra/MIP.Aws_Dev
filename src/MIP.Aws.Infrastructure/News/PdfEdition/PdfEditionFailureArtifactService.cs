using System.Text;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfEditionFailureArtifactService(
    IServiceScopeFactory scopeFactory,
    IFileStorageService fileStorage,
    IPdfDiscoveryPageCaptureService pageCapture,
    PortalArtifactUrlBuilder artifactUrls,
    IOptions<StorageOptions> storageOptions,
    ILogger<PdfEditionFailureArtifactService> logger) : IPdfEditionFailureArtifactService
{
    public const string AuditEventKind = "PdfEditionFailureCapture";

    private const int MinScreenshotBytes = 1_024;

    public async Task EnsureFailureArtifactsAsync(
        Guid newsSourceId,
        Guid downloadJobId,
        string? pageUrl,
        string failureMessage,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var existingLogs = await db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId == downloadJobId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (await artifactUrls.HasUsableScreenshotAsync(existingLogs, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == newsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return;
        }

        if (source.SourceType is not (NewsSourceType.PublicPdf or NewsSourceType.PublicHtml)
            || !source.PdfDiscoveryEnabled)
        {
            return;
        }

        var targetUrl = PdfFailureCaptureUrlResolver.Resolve(source, pageUrl);
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return;
        }

        string? screenshotPath = null;
        string? htmlPath = null;
        try
        {
            if (source.UseHeadlessBrowser)
            {
                var capture = await pageCapture.CaptureAsync(targetUrl, useHeadlessBrowser: true, cancellationToken)
                    .ConfigureAwait(false);
                (screenshotPath, htmlPath) = await PersistCaptureAsync(
                    source,
                    downloadJobId,
                    capture.ScreenshotPath,
                    capture.HtmlSnapshotPath,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                htmlPath = await CaptureHtmlOnlyAsync(source, downloadJobId, targetUrl, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not capture PDF failure artifacts for source {SourceId}", newsSourceId);
        }

        if (string.IsNullOrWhiteSpace(screenshotPath) && string.IsNullOrWhiteSpace(htmlPath))
        {
            return;
        }

        db.PortalDownloadAuditLogs.Add(new PortalDownloadAuditLog
        {
            Id = Guid.NewGuid(),
            NewsSourceId = newsSourceId,
            DownloadJobId = downloadJobId,
            EventKind = AuditEventKind,
            Message = $"Captured failure page for diagnosis: {targetUrl}",
            FailureCode = failureCode ?? InferFailureCode(failureMessage),
            ScreenshotRelativePath = screenshotPath,
            HtmlSnapshotRelativePath = htmlPath,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string? Screenshot, string? Html)> PersistCaptureAsync(
        NewsSource source,
        Guid downloadJobId,
        string? captureScreenshotPath,
        string? captureHtmlPath,
        CancellationToken cancellationToken)
    {
        var folder = $"{storageOptions.Value.NewspapersRelativePath}/{SanitizeFolder(source.Name)}/failures/{downloadJobId:N}";
        string? screenshotPath = null;
        string? htmlPath = null;

        if (!string.IsNullOrWhiteSpace(captureHtmlPath))
        {
            var htmlBytes = await fileStorage.ReadAsync(captureHtmlPath, cancellationToken).ConfigureAwait(false);
            if (htmlBytes is { Length: > 0 })
            {
                var htmlWrite = await fileStorage.WriteAsync($"{folder}/failure-page.html", htmlBytes, cancellationToken)
                    .ConfigureAwait(false);
                htmlPath = htmlWrite.RelativeKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(captureScreenshotPath))
        {
            var pngBytes = await fileStorage.ReadAsync(captureScreenshotPath, cancellationToken).ConfigureAwait(false);
            if (pngBytes is { Length: >= MinScreenshotBytes })
            {
                var pngWrite = await fileStorage.WriteAsync($"{folder}/failure-page.png", pngBytes, cancellationToken)
                    .ConfigureAwait(false);
                screenshotPath = pngWrite.RelativeKey;
            }
            else
            {
                logger.LogWarning(
                    "Skipping unusable failure screenshot ({Bytes} bytes) for job {JobId}.",
                    pngBytes?.Length ?? 0,
                    downloadJobId);
            }
        }

        return (screenshotPath, htmlPath);
    }

    private async Task<string?> CaptureHtmlOnlyAsync(
        NewsSource source,
        Guid downloadJobId,
        string pageUrl,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MIP.Aws/1.0 (pdf-failure-capture; authorized-ingestion)");
        var html = await http.GetStringAsync(pageUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var folder = $"{storageOptions.Value.NewspapersRelativePath}/{SanitizeFolder(source.Name)}/failures/{downloadJobId:N}";
        var htmlWrite = await fileStorage.WriteAsync($"{folder}/failure-page.html", Encoding.UTF8.GetBytes(html), cancellationToken)
            .ConfigureAwait(false);
        return htmlWrite.RelativeKey;
    }

    private static string SanitizeFolder(string name) =>
        string.Join('-', name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Replace(' ', '-');

    private static string InferFailureCode(string failureMessage) =>
        failureMessage.Contains("HTML", StringComparison.OrdinalIgnoreCase)
            ? "HtmlValidationFailed"
            : failureMessage.Contains("PDF", StringComparison.OrdinalIgnoreCase)
                ? "PdfValidationFailed"
                : "PdfDiscoveryFailed";
}
