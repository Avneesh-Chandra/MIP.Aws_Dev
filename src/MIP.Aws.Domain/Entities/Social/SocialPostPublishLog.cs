using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Social;

public sealed class SocialPostPublishLog : AuditableEntity
{
    public Guid SocialPostId { get; set; }

    public SocialPost? SocialPost { get; set; }

    public SocialPlatform Platform { get; set; }

    public Guid? SocialPlatformAccountId { get; set; }

    public bool Success { get; set; }

    public string? ExternalPostId { get; set; }

    public string? ExternalPostUrl { get; set; }

    public string? FailureReason { get; set; }

    public bool IsSimulated { get; set; }

    public DateTimeOffset AttemptedAt { get; set; }

    public Guid? ActorUserId { get; set; }
}
