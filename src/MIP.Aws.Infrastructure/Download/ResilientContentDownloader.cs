using System.Net.Http.Headers;
using MIP.Aws.Application.Connectors;

namespace MIP.Aws.Infrastructure.Download;

public sealed class ResilientContentDownloader(HttpClient httpClient) : IContentDownloader
{
    public async Task<DownloadedContent> DownloadAsync(Uri resource, IReadOnlyDictionary<string, string>? extraHeaders, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, resource);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        if (extraHeaders is not null)
        {
            foreach (var pair in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var status = (int)response.StatusCode;
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Content.Headers)
        {
            headers[h.Key] = string.Join(",", h.Value);
        }

        return new DownloadedContent(resource, payload, contentType, headers, status);
    }
}
