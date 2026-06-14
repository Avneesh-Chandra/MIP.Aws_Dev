namespace MIP.Aws.Application.Connectors;

/// <summary>
/// Low-level HTML/binary retrieval using HttpClient (static or lightly dynamic sites).
/// </summary>
public interface IContentDownloader
{
    /// <param name="extraHeaders">Optional per-request headers (for example Basic authentication).</param>
    Task<DownloadedContent> DownloadAsync(Uri resource, IReadOnlyDictionary<string, string>? extraHeaders, CancellationToken cancellationToken);
}
