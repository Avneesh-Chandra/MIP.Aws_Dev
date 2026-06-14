using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Social;

public sealed class SocialPostAttachment : AuditableEntity
{
    public Guid SocialPostId { get; set; }

    public SocialPost? SocialPost { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>e.g. Hero, QuoteCard, ExecutiveInsight, MarketSnapshot, Upload.</summary>
    public string AttachmentKind { get; set; } = "Upload";

    public string? PreviewUrl { get; set; }
}
