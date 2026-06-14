using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Social;

public sealed class SocialPostPlatform : AuditableEntity
{
    public Guid SocialPostId { get; set; }

    public SocialPost? SocialPost { get; set; }

    public SocialPlatform Platform { get; set; }

    public SocialContentVariant ContentVariant { get; set; }

    public Guid? SocialPlatformAccountId { get; set; }

    public SocialPlatformAccount? SocialPlatformAccount { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? Headline { get; set; }

    public string? CallToAction { get; set; }

    public string? HashtagsJson { get; set; }

    public string? MentionsJson { get; set; }

    public string? ComplianceNotes { get; set; }

    public decimal? AiConfidence { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool AiGenerated { get; set; }

    public string? ExternalPostId { get; set; }

    public string? ExternalPostUrl { get; set; }
}
