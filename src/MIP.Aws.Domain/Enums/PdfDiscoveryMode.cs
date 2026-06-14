namespace MIP.Aws.Domain.Enums;

/// <summary>
/// How public edition PDF links are discovered on publisher homepages.
/// </summary>
public enum PdfDiscoveryMode
{
    ManualSelector = 0,
    AutoDetectPdfLink = 1,
    KeywordBased = 2,
    Hybrid = 3
}
