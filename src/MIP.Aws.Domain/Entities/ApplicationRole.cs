using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Identity role with optional description and soft-delete support.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public bool IsDeleted { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
