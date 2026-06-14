using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class ArticleKeyword : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public string Keyword { get; set; } = string.Empty;

    public string Language { get; set; } = "und";

    public double Weight { get; set; }
}
