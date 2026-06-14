using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class ArticleSentiment : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public SentimentPolarity Polarity { get; set; }

    public double Confidence { get; set; }

    public string? Explanation { get; set; }

    public MarketImpactEstimate MarketImpact { get; set; } = MarketImpactEstimate.Unknown;
}
