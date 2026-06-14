using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Associates an extracted article span with physical newspaper pages.
/// </summary>
public class ArticlePage : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public int PageNumber { get; set; }

    public double? Confidence { get; set; }

    public string? Snippet { get; set; }
}
