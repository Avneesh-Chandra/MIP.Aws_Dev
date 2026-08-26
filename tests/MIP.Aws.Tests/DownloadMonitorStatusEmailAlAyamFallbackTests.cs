using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class DownloadMonitorStatusEmailAlAyamFallbackTests
{
    [Fact]
    public void Status_email_includes_AlAyam_publisher_epaper_link_when_today_pdf_missing()
    {
        var monitor = BuildMonitor(new DownloadMonitorSourceRowDto(
            Guid.NewGuid(),
            AlAyamPublicPdfBaseline.SourceName,
            "PublicPdf",
            "BH",
            "ar",
            "Failed",
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow,
            LatestPdfFileId: null,
            LatestDownloadJobId: Guid.NewGuid(),
            FailureReason: "Cloudflare blocked",
            FailureCode: "AccessDenied",
            ManualInterventionRequired: true,
            AdminInformed: false,
            InformAdminDisabled: false,
            SuggestedIntervention: "Open e-paper",
            EditionDateMatchesMonitor: false));

        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(
            monitor,
            portalBaseUrl: "https://portal.example.com");

        Assert.Contains(AlAyamPublicPdfBaseline.EpaperUrl, html, StringComparison.Ordinal);
        Assert.Contains("Al Ayam e-paper", html, StringComparison.Ordinal);
        Assert.Contains("not saved in MIP today", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_email_keeps_Not_available_for_non_AlAyam_without_pdf()
    {
        var monitor = BuildMonitor(new DownloadMonitorSourceRowDto(
            Guid.NewGuid(),
            "Kuwait - Al Qabas",
            "PublicPdf",
            "KW",
            "ar",
            "Failed",
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow,
            LatestPdfFileId: null,
            LatestDownloadJobId: Guid.NewGuid(),
            FailureReason: "timeout",
            FailureCode: null,
            ManualInterventionRequired: false,
            AdminInformed: false,
            InformAdminDisabled: false,
            SuggestedIntervention: null));

        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(
            monitor,
            portalBaseUrl: "https://portal.example.com");

        Assert.Contains("Not available", html, StringComparison.Ordinal);
        Assert.DoesNotContain(AlAyamPublicPdfBaseline.EpaperUrl, html, StringComparison.Ordinal);
    }

    private static DownloadMonitorDto BuildMonitor(DownloadMonitorSourceRowDto row) =>
        new(
            DateOnly.FromDateTime(DateTime.UtcNow),
            new DownloadMonitorSummaryDto(1, 0, 1, 1, 0, 0, 0, []),
            [row]);
}
