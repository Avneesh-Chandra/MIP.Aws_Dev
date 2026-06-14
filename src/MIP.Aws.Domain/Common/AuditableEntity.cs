namespace MIP.Aws.Domain.Common;

/// <summary>
/// Base type for tenant-aware, auditable aggregates with optimistic concurrency and soft delete.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>
    /// SQL Server rowversion mapped for optimistic concurrency.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
