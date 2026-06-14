using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Stores references to secrets (Azure Key Vault secret names); never persist raw credentials in the database.
/// </summary>
public class SourceCredential : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public string? KeyVaultSecretName { get; set; }

    public string? ApiKeyHeaderName { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Data-protection payload (not plaintext) for basic-auth style credentials when Key Vault is not used.
    /// </summary>
    public string? ProtectedCredentialPayload { get; set; }
}
