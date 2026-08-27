using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>Resolves the best screenshot/HTML snapshot paths from portal audit rows for a download job.</summary>
public static class PortalAuditArtifactResolver
{
    /// <summary>
    /// Playwright can write thousands of console/network rows per job. Keep a bounded window while
    /// retaining enough rows for the download chronology and failure artifacts.
    /// </summary>
    public const int MaxLogsPerJob = 120;

    public static (string? ScreenshotRelativePath, string? HtmlSnapshotRelativePath) ResolvePaths(
        IReadOnlyList<PortalDownloadAuditLog> logs)
    {
        var screenshot = logs
            .Where(a => !string.IsNullOrWhiteSpace(a.ScreenshotRelativePath))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.ScreenshotRelativePath)
            .FirstOrDefault();

        var html = logs
            .Where(a => !string.IsNullOrWhiteSpace(a.HtmlSnapshotRelativePath))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.HtmlSnapshotRelativePath)
            .FirstOrDefault();

        return (screenshot, html);
    }

    public static string? ToArtifactUrl(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? null
            : $"/api/v1/portal-artifacts?key={Uri.EscapeDataString(relativePath)}";
}
