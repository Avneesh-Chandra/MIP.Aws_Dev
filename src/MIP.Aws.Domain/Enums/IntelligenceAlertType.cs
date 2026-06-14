namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Categories of automated intelligence and operational alerts.
/// </summary>
public enum IntelligenceAlertType
{
    NegativeGfhSentiment = 1,
    RegulatoryNews = 2,
    CompetitorActivity = 3,
    MarketVolatility = 4,
    FailedDownload = 5,
    OcrFailure = 6,
    AiProcessingFailure = 7,
    General = 99
}
