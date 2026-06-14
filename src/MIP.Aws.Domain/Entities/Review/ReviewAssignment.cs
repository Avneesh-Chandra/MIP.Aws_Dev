using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Review;

/// <summary>
/// Routes an article to a specific analyst with an SLA deadline. Multiple assignments can exist
/// (e.g. reassignment) — the "current" one is identified by <see cref="ReviewAssignmentStatus.Pending"/>
/// or <see cref="ReviewAssignmentStatus.Accepted"/>.
/// </summary>
public class ReviewAssignment : AuditableEntity
{
    public Guid ExtractedArticleId { get; set; }

    public ExtractedArticle ExtractedArticle { get; set; } = null!;

    public Guid AssigneeUserId { get; set; }

    public string AssigneeEmail { get; set; } = string.Empty;

    public Guid AssignedByUserId { get; set; }

    public string AssignedByEmail { get; set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ReviewAssignmentStatus Status { get; set; } = ReviewAssignmentStatus.Pending;

    public string? Notes { get; set; }
}
