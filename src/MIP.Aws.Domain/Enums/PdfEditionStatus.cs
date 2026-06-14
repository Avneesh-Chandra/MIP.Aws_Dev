namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Lifecycle status for a public PDF edition discovery/download record.
/// </summary>
public enum PdfEditionStatus
{
    Discovered = 0,
    Validated = 1,
    Downloaded = 2,
    Failed = 3,
    BlockedByCompliance = 4,
    SkippedDuplicate = 5,
    NoPublicPdfAvailable = 6
}
