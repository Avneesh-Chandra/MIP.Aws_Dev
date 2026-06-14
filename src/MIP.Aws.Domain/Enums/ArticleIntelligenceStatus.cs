namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Tracks OCR/segmentation/AI enrichment for articles produced from licensed downloads.
/// </summary>
public enum ArticleIntelligenceStatus
{
    NotApplicable = 0,
    PendingOcr = 1,
    OcrComplete = 2,
    Segmented = 3,
    AiPending = 4,
    AiComplete = 5,
    Failed = 99
}
