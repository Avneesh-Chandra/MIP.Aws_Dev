using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Named entity extracted from an article (people, organizations, tickers, locations).
/// </summary>
public class ArticleEntity : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public string EntityType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public double Confidence { get; set; }
}
