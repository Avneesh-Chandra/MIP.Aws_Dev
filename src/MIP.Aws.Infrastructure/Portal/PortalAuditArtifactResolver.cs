using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>Resolves the best screenshot/HTML snapshot paths from portal audit rows for a download job.</summary>
public static class PortalAuditArtifactResolver
{
    /// <summary>
    /// Playwright can write thousands of console/network rows per job. Loading them all times out RDS.
    /// </summary>
    public const int MaxLogsPerJob = 40;

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
