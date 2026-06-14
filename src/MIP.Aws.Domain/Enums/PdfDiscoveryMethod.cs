namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Technique used to locate a PDF edition candidate.
/// </summary>
public enum PdfDiscoveryMethod
{
    Unknown = 0,
    ConfiguredLinkSelector = 1,
    ConfiguredDownloadSelector = 2,
    DirectPdfHref = 3,
    KeywordMatch = 4,
    PlaywrightClick = 5,
    PlaywrightPopup = 6,
    AutoScan = 7,
    ManualOverride = 8
}
