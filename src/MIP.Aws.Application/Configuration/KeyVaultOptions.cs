namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Azure Key Vault binding for production-grade secret management. Configured names map onto the
/// regular configuration tree via <c>KeyVaultConfigurationProvider</c>, so consumers keep using
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> unchanged.
/// </summary>
public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    /// <summary>Master switch — when false no secrets are pulled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Full vault URI, e.g. <c>https://gfh-mi-prod.vault.azure.net/</c>.</summary>
    public string? VaultUri { get; set; }

    /// <summary>Tenant id used by <c>DefaultAzureCredential</c> overrides (optional).</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional managed identity client id when multiple identities are bound.</summary>
    public string? ManagedIdentityClientId { get; set; }

    /// <summary>Refresh interval in minutes used by the Key Vault configuration provider.</summary>
    public int ReloadIntervalMinutes { get; set; } = 30;
}
