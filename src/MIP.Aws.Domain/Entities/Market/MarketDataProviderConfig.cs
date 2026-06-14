using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Market;

/// <summary>
/// Configuration record for one market data provider channel — encrypted secrets are NEVER
/// stored here; only references (e.g. an Azure Key Vault secret name) and metadata that the
/// runtime adapter can resolve at call time.
/// </summary>
public class MarketDataProviderConfig : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public MarketDataProviderType Provider { get; set; }

    /// <summary>Vendor-specific base URL when <see cref="Provider"/> is <c>ApprovedApi</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Logical reference to a secret (Key Vault name, env var) — not the secret itself.</summary>
    public string? AuthSecretReference { get; set; }

    /// <summary>JSON bag of vendor-specific knobs (rate limits, region, account tier).</summary>
    public string? OptionsJson { get; set; }

    /// <summary>Cron expression for scheduled API pulls (null for non-scheduled providers).</summary>
    public string? Schedule { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Last successful pull timestamp — diagnostic only.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }

    public DateTimeOffset? LastFailureAt { get; set; }

    public string? LastFailureMessage { get; set; }
}
