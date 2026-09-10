using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Enums;
using Xunit;

namespace MIP.Aws.Tests;

public sealed class PdfLastDownloadedThroughFormatterTests
{
    [Fact]
    public void Formats_scheduled_as_mip_batch()
    {
        var text = PdfLastDownloadedThroughFormatter.Format(
            DownloadJobTrigger.Scheduled,
            DownloadJobStatus.Succeeded,
            PdfDiscoveryMethod.DirectPdfHref,
            "today-edition.pdf",
            new DateTimeOffset(2026, 9, 10, 4, 33, 0, TimeSpan.Zero));

        Assert.Equal("MIP batch", text);
    }

    [Fact]
    public void Formats_manual_upload_as_mip_manual()
    {
        var text = PdfLastDownloadedThroughFormatter.Format(
            DownloadJobTrigger.Manual,
            DownloadJobStatus.Succeeded,
            PdfDiscoveryMethod.ManualOverride,
            "operator-upload.pdf",
            new DateTimeOffset(2026, 9, 10, 4, 33, 0, TimeSpan.Zero));

        Assert.Equal("MIP Manual", text);
    }

    [Fact]
    public void Formats_auto_ai_as_mip_auto_recovery()
    {
        var text = PdfLastDownloadedThroughFormatter.Format(
            DownloadJobTrigger.AutoAiRecovery,
            DownloadJobStatus.SuccessWithAutoAiRecovery,
            PdfDiscoveryMethod.DirectPdfHref,
            "today-edition.pdf",
            new DateTimeOffset(2026, 9, 10, 4, 33, 0, TimeSpan.Zero));

        Assert.Equal("MIP Auto recovery", text);
    }

    [Fact]
    public void Formats_external_rpa_as_pad_with_timestamp()
    {
        var text = PdfLastDownloadedThroughFormatter.Format(
            DownloadJobTrigger.ExternalRpa,
            DownloadJobStatus.Succeeded,
            PdfDiscoveryMethod.ManualOverride,
            "INAF_20260908231949999.pdf",
            new DateTimeOffset(2026, 9, 10, 4, 52, 0, TimeSpan.Zero),
            "Asia/Bahrain");

        Assert.Equal("PAD on 2026-09-10 07:52 Bahrain", text);
    }

    [Fact]
    public void Treats_legacy_inaf_manual_override_as_pad()
    {
        var text = PdfLastDownloadedThroughFormatter.Format(
            DownloadJobTrigger.Manual,
            DownloadJobStatus.Succeeded,
            PdfDiscoveryMethod.ManualOverride,
            "INAF_20260908231949999 (3).pdf",
            new DateTimeOffset(2026, 9, 10, 4, 52, 0, TimeSpan.Zero),
            "Asia/Bahrain");

        Assert.Equal("PAD on 2026-09-10 07:52 Bahrain", text);
    }
}
