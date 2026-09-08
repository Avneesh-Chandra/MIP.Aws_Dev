namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Optional outbound HTTP settings for publisher downloads (AWS-only; no cross-cloud relay).
/// </summary>
public sealed class PublisherOutboundOptions
{
    public const string SectionName = "PublisherOutbound";

    public AlAyamOutboundClientOptions AlAyam { get; set; } = new();
}

public sealed class AlAyamOutboundClientOptions
{
    /// <summary>
    /// Optional HTTP/HTTPS forward proxy for Al Ayam fetches when AWS egress is blocked
    /// (commercial proxy, NAT gateway with allowlisted IP, etc.).
    /// </summary>
    public string? HttpProxyUri { get; set; }

    /// <summary>
    /// When true, ECS/Hangfire does not launch Playwright for Al Ayam and waits for an RPA upload.
    /// </summary>
    public bool UseExternalRpa { get; set; }

    /// <summary>
    /// Shared secret for <c>X-Rpa-Api-Key</c> on the today-PDF ingest endpoint. Empty disables the key.
    /// </summary>
    public string? IngestApiKey { get; set; }

    /// <summary>
    /// When set, RPA ingest is limited to this news source id (Al Ayam in each environment).
    /// </summary>
    public Guid? IngestAllowedSourceId { get; set; }
}
