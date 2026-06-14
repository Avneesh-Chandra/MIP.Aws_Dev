using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Social;

/// <summary>GFH-approved original social content derived from executive briefs (not raw newspaper text).</summary>
public sealed class SocialPost : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string? InternalNotes { get; set; }

    public Guid? ExecutiveBriefId { get; set; }

    public Guid? ExtractedArticleId { get; set; }

    public SocialStudioSourceType SourceType { get; set; } = SocialStudioSourceType.Article;

    public SocialPostStatus Status { get; set; } = SocialPostStatus.Draft;

    public int? ComplianceScore { get; set; }

    public string? ComplianceRiskLevel { get; set; }

    public string? ComplianceIssuesJson { get; set; }

    public decimal? AiConfidence { get; set; }

    public DateTimeOffset? ComplianceCheckedAt { get; set; }

    public DateTimeOffset? AiGeneratedAt { get; set; }

    public bool ComplianceAcknowledged { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public DateTimeOffset? ScheduledAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? LastFailureReason { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public ICollection<SocialPostPlatform> Platforms { get; set; } = new List<SocialPostPlatform>();

    public ICollection<SocialPostAttachment> Attachments { get; set; } = new List<SocialPostAttachment>();

    public ICollection<SocialPostApproval> Approvals { get; set; } = new List<SocialPostApproval>();

    public ICollection<SocialPostPublishLog> PublishLogs { get; set; } = new List<SocialPostPublishLog>();
}
