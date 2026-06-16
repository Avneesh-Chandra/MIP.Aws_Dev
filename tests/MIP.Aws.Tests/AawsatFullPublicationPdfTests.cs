using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using MIP.Aws.Infrastructure.News.PdfEdition;
using Microsoft.Extensions.Logging.Abstractions;

namespace MIP.Aws.Tests;

public sealed class AawsatFullPublicationPdfTests
{
    [Theory]
    [InlineData("https://aawsat.com/files/pdf/issue17367/", "https://aawsat.com/files/pdf/issue17367/index.html")]
    [InlineData("https://aawsat.com/files/pdf/issue17367/2", "https://aawsat.com/files/pdf/issue17367/2/index.html")]
    [InlineData("https://aawsat.com/files/pdf/issue17367/2/index.html", "https://aawsat.com/files/pdf/issue17367/2/index.html")]
    public void ResolveIssueViewerUri_opens_main_shell_not_single_page_folder(string input, string expected)
    {
        var resolved = AawsatFullPublicationPdf.ResolveIssueViewerUri(new Uri(input));

        Assert.Equal(expected, resolved.ToString());
    }

    [Fact]
    public void PdfFailureCaptureUrlResolver_prefers_issue_viewer_for_aawsat()
    {
        var source = BuildAawsatSource();
        var hinted = "https://aawsat.com/files/pdf/issue17367/";

        var captureUrl = PdfFailureCaptureUrlResolver.Resolve(source, hinted);

        Assert.Equal("https://aawsat.com/files/pdf/issue17367/index.html", captureUrl);
    }

    [Fact]
    public void PdfFailureCaptureUrlResolver_falls_back_to_homepage_when_no_issue_hint()
    {
        var source = BuildAawsatSource();

        var captureUrl = PdfFailureCaptureUrlResolver.Resolve(source, hintedPageUrl: null);

        Assert.Equal(AawsatPublicPdfBaseline.PdfDiscoveryPageUrl, captureUrl);
    }

    [Fact]
    public void ResolveTimeoutMs_clamps_source_wait_seconds()
    {
        var source = BuildAawsatSource();
        source.DownloadWaitTimeoutSeconds = 240;

        Assert.Equal(240_000, AawsatFullPublicationPdf.ResolveTimeoutMs(source));
    }

    [Fact]
    public async Task TryDownloadBytes_live_issue_viewer_returns_pdf_when_enabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MIP_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var source = BuildAawsatSource();
        var issueUri = new Uri("https://aawsat.com/files/pdf/issue17367/");

        var bytes = await AawsatFullPublicationPdf.TryDownloadBytesAsync(
            issueUri,
            source,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 50_000, $"Expected a multi-page PDF, got {bytes.Length} bytes.");
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    private static NewsSource BuildAawsatSource() => new()
    {
        Id = Guid.NewGuid(),
        Name = AawsatPublicPdfBaseline.SourceName,
        ConnectorKey = AawsatEditionDiscovery.Key,
        SourceType = NewsSourceType.PublicHtml,
        PdfDiscoveryEnabled = true,
        UseHeadlessBrowser = true,
        PdfDownloadSelector = AawsatPublicPdfBaseline.PdfDownloadSelector,
        PdfLinkSelector = AawsatPublicPdfBaseline.PdfLinkSelector,
        BaseUrl = AawsatPublicPdfBaseline.BaseUrl,
        EditionUrl = AawsatPublicPdfBaseline.EditionUrl,
        PdfDiscoveryPageUrl = AawsatPublicPdfBaseline.PdfDiscoveryPageUrl,
        DownloadWaitTimeoutSeconds = 180
    };
}
