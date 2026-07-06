using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class DownloadMonitorStatusEmailActionHelperTests
{
    [Fact]
    public void BuildInformAdminMailTo_returns_null_for_successful_rows()
    {
        var row = new DownloadMonitorSourceRowDto(
            Guid.NewGuid(),
            "UAE - Al Khaleej",
            "PublicPdf",
            "AE",
            null,
            DownloadMonitorStatusLabels.Success,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            ManualInterventionRequired: false,
            AdminInformed: false,
            InformAdminDisabled: false,
            SuggestedIntervention: null);

        var mailTo = DownloadMonitorStatusEmailActionHelper.BuildInformAdminMailTo(
            "admin_mip@gfh.com",
            row,
            new DateOnly(2026, 7, 1),
            null);

        Assert.Null(mailTo);
    }

    [Fact]
    public void BuildInformAdminMailTo_includes_failure_and_recovery_details()
    {
        var jobId = Guid.NewGuid();
        var row = new DownloadMonitorSourceRowDto(
            Guid.NewGuid(),
            "Bahrain - Al Ayam",
            "PublicPdf",
            "BH",
            null,
            DownloadMonitorStatusLabels.FailedAfterAutoAiRecovery,
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow,
            null,
            jobId,
            "PDF validation failed",
            "PdfValidationFailed",
            ManualInterventionRequired: true,
            AdminInformed: false,
            InformAdminDisabled: false,
            SuggestedIntervention: "Review recovery details and inform Admin.");

        var context = new DownloadMonitorEmailFailureContext(
            "All eligible suggestions were tried.",
            [new DownloadMonitorEmailTimelineStep("Apply suggestion", "Restore baseline selector", DateTimeOffset.UtcNow)]);

        var mailTo = DownloadMonitorStatusEmailActionHelper.BuildInformAdminMailTo(
            "admin_mip@gfh.com",
            row,
            new DateOnly(2026, 7, 1),
            context);

        Assert.NotNull(mailTo);
        Assert.StartsWith("mailto:admin_mip%40gfh.com?to=admin_mip%40gfh.com", mailTo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Manual%20intervention%20required", mailTo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PdfValidationFailed", mailTo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDetailsUrl_uses_absolute_portal_link_with_job_id()
    {
        var jobId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var url = DownloadMonitorStatusEmailActionHelper.BuildDetailsUrl(
            "https://d3rvv409o9wpf6.cloudfront.net",
            jobId);

        Assert.Equal(
            "https://d3rvv409o9wpf6.cloudfront.net/operator/download-monitor?jobId=11111111-1111-1111-1111-111111111111",
            url);
    }

    [Fact]
    public void BuildInformAdminPortalUrl_includes_inform_admin_query_flag()
    {
        var jobId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var url = DownloadMonitorStatusEmailActionHelper.BuildInformAdminPortalUrl(
            "https://d3rvv409o9wpf6.cloudfront.net",
            jobId);

        Assert.Equal(
            "https://d3rvv409o9wpf6.cloudfront.net/operator/download-monitor?jobId=11111111-1111-1111-1111-111111111111&informAdmin=1",
            url);
    }

    [Fact]
    public void EmailHtmlLinkFormatter_mailto_link_preserves_query_separators()
    {
        var html = EmailHtmlLinkFormatter.Link("mailto:admin@test.com?subject=Hello&body=World", "Inform Admin");
        Assert.Contains("href=\"mailto:admin@test.com?subject=Hello&body=World\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;body=", html, StringComparison.Ordinal);
    }
}
