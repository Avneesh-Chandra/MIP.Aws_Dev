using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Known-good public PDF settings for Bahrain - Al Ayam (local recovery test baseline).</summary>
public static class AlAyamPublicPdfBaseline
{
    public const string SourceName = "Bahrain - Al Ayam";
    public const string ConnectorKey = "news.alayam";
    public const string EpaperUrl = "https://www.alayam.com/epaper";
    public const string PdfLinkSelector = "a#aPDFdownloadAllPages, a:has-text('كل الصفحات')";

    public static class Broken
    {
        public const string PdfLinkSelector = "a#brokenRecoveryTestLink, a:has-text('INVALID DOWNLOAD')";
        public const string EpaperUrl = "https://www.alayam.com/epaper-recovery-test-broken";
    }

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
        null,
        PdfLinkSelector,
        EpaperUrl,
        EpaperUrl,
        EpaperUrl,
        180,
        true);
}
