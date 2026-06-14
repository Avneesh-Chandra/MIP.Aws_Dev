using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Curated executive intelligence item — exactly one per escalated article. Carries the analyst's
/// priority, impact rating, executive notes, and recommendations.
/// </summary>
public class ExecutiveQueueItem : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public ExecutivePriority Priority { get; set; } = ExecutivePriority.Medium;

    public MarketImpactEstimate ImpactLevel { get; set; } = MarketImpactEstimate.Unknown;

    public string? ExecutiveNote { get; set; }

    public string? Recommendation { get; set; }

    public Guid EscalatedByUserId { get; set; }

    public string EscalatedByEmail { get; set; } = string.Empty;

    public DateTimeOffset EscalatedAt { get; set; }

    /// <summary>Manual sort order on the executive queue board (lower = nearer the top).</summary>
    public int DisplayOrder { get; set; }

    public bool IsPublishedToBrief { get; set; }

    public Guid? PublishedToBriefId { get; set; }

    public ExecutiveBrief? PublishedToBrief { get; set; }
}
