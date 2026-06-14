using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Lightweight inline annotation / tag / bookmark / pinned note attached to a specific span of an article.
/// </summary>
public class ArticleAnnotation : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public Guid AuthorUserId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>One of: highlight | tag | bookmark | note.</summary>
    public string Kind { get; set; } = "highlight";

    /// <summary>Free-form label/tag value (max ~128 chars).</summary>
    public string? Label { get; set; }

    public string? Note { get; set; }

    /// <summary>Inclusive character offset into the cleaned article body (null = entire article).</summary>
    public int? StartOffset { get; set; }

    /// <summary>Exclusive character offset; pairs with <see cref="StartOffset"/>.</summary>
    public int? EndOffset { get; set; }

    /// <summary>The text snapshot at the time of annotation (so cleaned-content edits don't orphan the highlight).</summary>
    public string? AnchorText { get; set; }

    public bool IsPinned { get; set; }
}
