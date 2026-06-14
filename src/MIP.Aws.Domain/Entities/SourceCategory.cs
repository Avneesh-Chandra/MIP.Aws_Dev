using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class SourceCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<NewsSource> NewsSources { get; set; } = new List<NewsSource>();
}
