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
}
