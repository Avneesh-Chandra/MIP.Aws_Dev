namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Security-relevant audit trail for identity operations (immutable rows).
/// </summary>
public class UserAuditLog
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
