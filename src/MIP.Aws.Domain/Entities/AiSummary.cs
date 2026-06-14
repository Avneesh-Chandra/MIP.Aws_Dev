using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Persisted AI analysis payload for an article (kept separate from editorial content).
/// </summary>
public class AiSummary : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public string ModelName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Sentiment { get; set; } = string.Empty;

    public double SentimentConfidence { get; set; }

    public string? SentimentRationale { get; set; }

    public string? ExecutiveNarrative { get; set; }

    public double GfhRelevanceScore { get; set; }

    public string? TopicsJson { get; set; }

    public string? KeywordsJson { get; set; }

    public string? RiskSignalsJson { get; set; }

    public string? OpportunitySignalsJson { get; set; }
}
