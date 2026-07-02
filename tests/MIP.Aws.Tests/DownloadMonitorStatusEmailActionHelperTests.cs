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
        Assert.StartsWith("mailto:admin_mip%40gfh.com", mailTo, StringComparison.OrdinalIgnoreCase);
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
}
