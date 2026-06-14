using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Executive;

public sealed class DailyExecutiveBriefItem : AuditableEntity
{
    public Guid DailyExecutiveBriefId { get; set; }

    public DailyExecutiveBrief? DailyExecutiveBrief { get; set; }

    public Guid? ExtractedArticleId { get; set; }

    public DailyExecutiveBriefSectionType SectionType { get; set; }

    public string Headline { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? SourceName { get; set; }

    public string? SourceUrl { get; set; }

    public decimal? ImportanceScore { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsIncluded { get; set; } = true;
}
