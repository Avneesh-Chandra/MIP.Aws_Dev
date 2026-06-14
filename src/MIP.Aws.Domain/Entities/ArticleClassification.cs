using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class ArticleClassification : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public NewsTopicCategory Category { get; set; }

    public double Confidence { get; set; }

    public bool IsPrimary { get; set; }
}
