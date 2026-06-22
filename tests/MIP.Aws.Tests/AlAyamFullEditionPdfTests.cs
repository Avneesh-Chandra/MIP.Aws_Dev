using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Tests;

public sealed class AlAyamFullEditionPdfTests
{
    [Theory]
    [InlineData("https://i.alayam.com/ayamnewsa/upload/issue/2026/13584/PDF/INAF_20260617000803888.pdf", true)]
    [InlineData("https://www.alayam.com/epaper", false)]
    [InlineData("https://i.alayam.com/other/file.pdf", false)]
    public void IsDirectPdfUrl_recognizes_inaf_issue_pdfs(string url, bool expected)
    {
        Assert.Equal(expected, AlAyamFullEditionPdf.IsDirectPdfUrl(new Uri(url)));
    }

    [Fact]
    public void UsesClickPath_matches_alayam_connector()
    {
        var source = BuildAlAyamSource();
        Assert.True(AlAyamFullEditionPdf.UsesClickPath(source));
    }

    [Fact]
    public void PdfFailureCaptureUrlResolver_returns_epaper_for_alayam()
    {
        var source = BuildAlAyamSource();
        var captureUrl = PdfFailureCaptureUrlResolver.Resolve(source, hintedPageUrl: null);
        Assert.Equal(AlAyamFullEditionPdf.EpaperUrl, captureUrl);
    }

    [Fact]
    public void ExtractPdfUrlFromHtml_finds_all_pages_anchor_and_inaf_regex()
    {
        const string html = """
            <a id="aPDFdownloadAllPages" href="https://i.alayam.com/ayamnewsa/upload/issue/2026/13584/PDF/INAF_20260617000803888.pdf">كل الصفحات</a>
            """;

        var url = AlAyamFullEditionPdf.ExtractPdfUrlFromHtml(html);

        Assert.NotNull(url);
        Assert.Equal(
            "https://i.alayam.com/ayamnewsa/upload/issue/2026/13584/PDF/INAF_20260617000803888.pdf",
            url!.ToString());
    }

    [Fact]
    public void ResolveTimeoutMs_clamps_source_wait_seconds()
    {
        var source = BuildAlAyamSource();
        source.DownloadWaitTimeoutSeconds = 90;
        Assert.Equal(90_000, AlAyamFullEditionPdf.ResolveTimeoutMs(source));
    }

    private static NewsSource BuildAlAyamSource() => new()
    {
        Id = Guid.NewGuid(),
        Name = AlAyamPublicPdfBaseline.SourceName,
        ConnectorKey = AlAyamEditionDiscovery.Key,
        SourceType = NewsSourceType.PublicHtml,
        PdfDiscoveryEnabled = true,
        UseHeadlessBrowser = true,
        PdfLinkSelector = AlAyamPublicPdfBaseline.PdfLinkSelector,
        PdfDiscoveryPageUrl = AlAyamPublicPdfBaseline.EpaperUrl,
        EditionUrl = AlAyamPublicPdfBaseline.EpaperUrl,
        BaseUrl = AlAyamPublicPdfBaseline.EpaperUrl,
        DownloadWaitTimeoutSeconds = 120
    };
}
