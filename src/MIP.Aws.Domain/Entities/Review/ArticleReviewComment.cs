using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Threaded analyst comment on an article. Supports @mentions and parent threading.
/// </summary>
public class ArticleReviewComment : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public Guid AuthorUserId { get; set; }

    public string AuthorEmail { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Optional comma-separated user IDs that were @-mentioned.</summary>
    public string? MentionedUserIds { get; set; }

    public Guid? ParentCommentId { get; set; }

    public ArticleReviewComment? ParentComment { get; set; }

    public bool IsPinned { get; set; }

    public bool IsResolved { get; set; }
}
