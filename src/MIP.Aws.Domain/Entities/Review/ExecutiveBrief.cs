using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Analyst-curated executive intelligence brief — collection of approved articles with ordering and commentary.
/// </summary>
public class ExecutiveBrief : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? IntroNarrative { get; set; }

    public string? ClosingNotes { get; set; }

    public ExecutiveBriefStatus Status { get; set; } = ExecutiveBriefStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public string? PublishedByEmail { get; set; }

    /// <summary>Optional storage key referencing the rendered PDF artifact.</summary>
    public string? RenderedPdfRelativePath { get; set; }

    /// <summary>Optional storage key referencing the rendered HTML snapshot.</summary>
    public string? RenderedHtmlRelativePath { get; set; }

    public ICollection<ExecutiveBriefItem> Items { get; set; } = new List<ExecutiveBriefItem>();
}

/// <summary>
/// Article entry inside an <see cref="ExecutiveBrief"/> — preserves order and analyst commentary.
/// </summary>
public class ExecutiveBriefItem : AuditableEntity
{
    public Guid ExecutiveBriefId { get; set; }

    public ExecutiveBrief ExecutiveBrief { get; set; } = null!;

    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string? Commentary { get; set; }
}
