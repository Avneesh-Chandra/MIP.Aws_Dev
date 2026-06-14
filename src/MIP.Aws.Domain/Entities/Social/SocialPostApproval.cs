using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Social;

public sealed class SocialPostApproval : AuditableEntity
{
    public Guid SocialPostId { get; set; }

    public SocialPost? SocialPost { get; set; }

    public bool IsApproval { get; set; }

    public string? Comments { get; set; }

    public Guid? ActorUserId { get; set; }

    public DateTimeOffset ActionAt { get; set; }
}
