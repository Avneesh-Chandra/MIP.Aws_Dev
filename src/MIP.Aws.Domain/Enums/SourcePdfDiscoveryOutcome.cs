namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Last-known PDF discovery outcome for a news source (UI status).
/// </summary>
public enum SourcePdfDiscoveryOutcome
{
    Unknown = 0,
    RealPdfFound = 1,
    NoPublicPdfAvailable = 2
}
