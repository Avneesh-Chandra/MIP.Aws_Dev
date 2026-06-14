using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Known-good public PDF settings for Asharq Al-Awsat (issue viewer → Download → Full Publication).</summary>
public static class AawsatPublicPdfBaseline
{
    public const string SourceName = "Asharq Al-Awsat";
    public const string ConnectorKey = "news.aawsat";
    public const string BaseUrl = "https://aawsat.com";
    public const string EditionUrl = "https://aawsat.com/files/pdf/";
    public const string PdfDiscoveryPageUrl = "https://aawsat.com";
    public const string PdfDownloadSelector = "button[aria-label='Download']";
    public const string PdfLinkSelector = "a:has-text('Full Publication')";

    public static SourceRecoveryConfigurationPatchDto RecoveryPatch() => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        PdfDownloadSelector,
        PdfLinkSelector,
        BaseUrl,
        EditionUrl,
        PdfDiscoveryPageUrl,
        180,
        true);
}
