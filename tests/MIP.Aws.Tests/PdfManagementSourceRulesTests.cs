using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Tests;

public sealed class PdfManagementSourceRulesTests
{
    [Fact]
    public void IsPdfDownloadMonitoredSource_includes_kuwait_al_qabas_when_pdf_discovery_enabled()
    {
        var source = new NewsSource
        {
            Name = AlQabasPublicPdfBaseline.SourceName,
            BaseUrl = AlQabasPublicPdfBaseline.BaseUrl,
            ConnectorKey = AlQabasPublicPdfBaseline.ConnectorKey,
            SourceType = NewsSourceType.PublicPdf,
            PdfDiscoveryEnabled = true,
            IsEnabled = true
        };

        Assert.True(PdfManagementSourceRules.IsPdfDownloadMonitoredSource(source));
    }

    [Fact]
    public void IsPdfDownloadMonitoredSource_excludes_al_qabas_when_pdf_discovery_disabled()
    {
        var source = new NewsSource
        {
            Name = AlQabasPublicPdfBaseline.SourceName,
            BaseUrl = AlQabasPublicPdfBaseline.BaseUrl,
            ConnectorKey = AlQabasPublicPdfBaseline.ConnectorKey,
            SourceType = NewsSourceType.PublicPdf,
            PdfDiscoveryEnabled = false,
            IsEnabled = true
        };

        Assert.False(PdfManagementSourceRules.IsPdfDownloadMonitoredSource(source));
    }
}
