namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Centralized feature flags. Flags default to safe values so a missing configuration section
/// never turns a feature on accidentally.
/// </summary>
public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Master toggle for the production observability stack (OpenTelemetry + App Insights).</summary>
    public bool UseObservability { get; set; } = true;

    /// <summary>When true, Redis is used for distributed caching; otherwise the in-memory adapter is used.</summary>
    public bool UseRedisCache { get; set; }

    /// <summary>When true, Azure Blob Storage replaces the local file storage backend.</summary>
    public bool UseAzureBlobStorage { get; set; }

    /// <summary>When true, configuration is augmented from Azure Key Vault at startup.</summary>
    public bool UseAzureKeyVault { get; set; }

    /// <summary>When true, rate limiting middleware is registered.</summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>When true, login throttling tracks brute-force attempts in the distributed cache.</summary>
    public bool EnableLoginThrottling { get; set; } = true;

    /// <summary>When true, HealthChecks UI is exposed at /health/ui.</summary>
    public bool EnableHealthChecksUi { get; set; } = true;

    /// <summary>When true, security headers middleware is installed in the request pipeline.</summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    /// <summary>When true, the immutable audit trail is persisted for administrative actions.</summary>
    public bool EnableImmutableAuditLog { get; set; } = true;
}
