using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>Builds portal-artifact API URLs only when the underlying blob exists in storage.</summary>
public sealed class PortalArtifactUrlBuilder(IFileStorageService fileStorage)
{
    private const int MinScreenshotBytes = 1_024;
    private const int MinHtmlBytes = 200;

    public async Task<(string? ScreenshotUrl, string? HtmlUrl)> BuildUrlsAsync(
        IReadOnlyList<PortalDownloadAuditLog> logs,
        CancellationToken cancellationToken)
    {
        var ordered = logs.OrderByDescending(a => a.CreatedAt).ToList();

        string? screenshotUrl = null;
        foreach (var log in ordered)
        {
            if (string.IsNullOrWhiteSpace(log.ScreenshotRelativePath))
            {
                continue;
            }

            if (await BlobLooksValidAsync(log.ScreenshotRelativePath, MinScreenshotBytes, cancellationToken).ConfigureAwait(false))
            {
                screenshotUrl = PortalAuditArtifactResolver.ToArtifactUrl(log.ScreenshotRelativePath);
                break;
            }
        }

        string? htmlUrl = null;
        foreach (var log in ordered)
        {
            if (string.IsNullOrWhiteSpace(log.HtmlSnapshotRelativePath))
            {
                continue;
            }

            if (await BlobLooksValidAsync(log.HtmlSnapshotRelativePath, MinHtmlBytes, cancellationToken).ConfigureAwait(false))
            {
                htmlUrl = PortalAuditArtifactResolver.ToArtifactUrl(log.HtmlSnapshotRelativePath);
                break;
            }
        }

        return (screenshotUrl, htmlUrl);
    }

    public async Task<bool> HasUsableScreenshotAsync(
        IReadOnlyList<PortalDownloadAuditLog> logs,
        CancellationToken cancellationToken)
    {
        foreach (var log in logs.OrderByDescending(a => a.CreatedAt))
        {
            if (string.IsNullOrWhiteSpace(log.ScreenshotRelativePath))
            {
                continue;
            }

            if (await BlobLooksValidAsync(log.ScreenshotRelativePath, MinScreenshotBytes, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> BlobLooksValidAsync(
        string relativePath,
        int minBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await fileStorage.ReadAsync(relativePath, cancellationToken).ConfigureAwait(false);
            return bytes is not null && bytes.Length >= minBytes;
        }
        catch
        {
            return false;
        }
    }
}
