namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Idempotent seeding of GFH newspaper catalog sources. Credentials are never hard-coded; use User Secrets or environment variables.
/// </summary>
public sealed class NewspaperCatalogOptions
{
    public const string SectionName = "NewspaperCatalog";

    /// <summary>When true, missing catalog sources are inserted on application startup (matched by base URL).</summary>
    public bool SeedOnStartup { get; set; } = true;

    /// <summary>
    /// Development-only: after seeding known-good Al Ayam settings, apply a deliberate misconfiguration
    /// so PDF download fails and AI recovery can be tested locally.
    /// </summary>
    public bool AlAyamRecoveryTestBreak { get; set; }

    public PressReaderCredentialOptions PressReader { get; set; } = new();
}

public sealed class PressReaderCredentialOptions
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
