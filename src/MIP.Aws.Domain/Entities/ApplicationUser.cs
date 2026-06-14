using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Application user backed by ASP.NET Core Identity with enterprise audit fields.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }

    public string? EntraObjectId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PreferredLanguage { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
