namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Azure Blob Storage binding used by the production storage backend. When <see cref="Enabled"/>
/// is false the existing local filesystem provider continues to serve requests.
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>Master switch — when true the <c>AzureBlobStorageService</c> is wired.</summary>
    public bool Enabled { get; set; }

    /// <summary>Connection string OR <see cref="ServiceUri"/> must be provided.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Service URI for managed identity authentication (preferred in Azure).</summary>
    public string? ServiceUri { get; set; }

    /// <summary>Optional managed identity client id.</summary>
    public string? ManagedIdentityClientId { get; set; }

    /// <summary>Container used for all platform artifacts.</summary>
    public string ContainerName { get; set; } = "gfh-media-intelligence";

    /// <summary>Default retention applied by lifecycle helpers (days).</summary>
    public int DefaultRetentionDays { get; set; } = 365;

    /// <summary>SAS link lifetime when creating temporary download URLs (minutes).</summary>
    public int SasLinkLifetimeMinutes { get; set; } = 15;

    /// <summary>Auto-create the container on startup when missing.</summary>
    public bool AutoCreateContainer { get; set; } = true;

    /// <summary>When true, server-side AES-256 is asserted explicitly on each upload (most accounts already enforce this).</summary>
    public bool RequireServerSideEncryption { get; set; } = true;
}
