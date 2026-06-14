namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Immutable audit trail rows (not soft-deleted; retained for compliance).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? IpAddress { get; set; }

    public string? DetailsJson { get; set; }
}
