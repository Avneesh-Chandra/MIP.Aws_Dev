using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Immutable workflow transition row — append-only history of state changes against an article.
/// </summary>
public class ArticleReviewAction : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public Guid ActorUserId { get; set; }

    public string ActorEmail { get; set; } = string.Empty;

    public ArticleReviewState FromState { get; set; }

    public ArticleReviewState ToState { get; set; }

    /// <summary>One of: approve | reject | escalate | assign | reassign | edit-ai | publish | archive | comment.</summary>
    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    /// <summary>Optional JSON capturing field-level overrides applied alongside the transition (for diff views).</summary>
    public string? OverridesJson { get; set; }
}
