namespace MIP.Aws.Application.Configuration;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    /// <summary>When true, seeds optional development/UAT role test accounts from <see cref="DevelopmentUsers"/>.</summary>
    public bool SeedDevelopmentRoleUsers { get; set; }

    /// <summary>When true, new users must change password on first login (production recommended).</summary>
    public bool RequirePasswordChangeOnFirstLogin { get; set; }

    public string DefaultAdminEmail { get; set; } = "superadmin@mip.local";

    /// <summary>Inject via environment, User Secrets, or AWS Secrets Manager — never commit.</summary>
    public string DefaultAdminPassword { get; set; } = string.Empty;

    public List<SeedDevelopmentUserOptions> DevelopmentUsers { get; set; } = [];
}

public sealed class SeedDevelopmentUserOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
