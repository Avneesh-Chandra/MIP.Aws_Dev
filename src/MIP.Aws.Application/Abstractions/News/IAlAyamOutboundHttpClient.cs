namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// Fetches Al Ayam publisher URLs via optional forward proxy or direct AWS egress.
/// </summary>
public interface IAlAyamOutboundHttpClient
{
    Task<AlAyamOutboundHttpResponse?> GetAsync(Uri url, CancellationToken cancellationToken);
}

public sealed record AlAyamOutboundHttpResponse(
    byte[] Body,
    string? ContentType,
    int StatusCode,
    string Transport);
