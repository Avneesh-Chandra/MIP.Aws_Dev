using System.Net;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class AlAyamOutboundHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<PublisherOutboundOptions> options,
    ILogger<AlAyamOutboundHttpClient> logger) : IAlAyamOutboundHttpClient
{
    internal const string DirectClientName = "AlAyamOutboundDirect";

    public async Task<AlAyamOutboundHttpResponse?> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!AlAyamPublisherHosts.IsAlAyamHost(url))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(DirectClientName);
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var transport = string.IsNullOrWhiteSpace(options.Value.AlAyam.HttpProxyUri) ? "direct" : "proxy";
            return new AlAyamOutboundHttpResponse(
                body,
                response.Content.Headers.ContentType?.MediaType,
                (int)response.StatusCode,
                transport);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Al Ayam outbound fetch failed for {Url}.", url);
            return null;
        }
    }

    internal static void ConfigureDirectHandler(IServiceProvider sp, SocketsHttpHandler handler)
    {
        var proxyUri = sp.GetRequiredService<IOptions<PublisherOutboundOptions>>().Value.AlAyam.HttpProxyUri;
        if (string.IsNullOrWhiteSpace(proxyUri) || !Uri.TryCreate(proxyUri, UriKind.Absolute, out var proxy))
        {
            handler.Proxy = null;
            handler.UseProxy = false;
            return;
        }

        handler.Proxy = new WebProxy(proxy);
        handler.UseProxy = true;
    }

    internal static readonly IAlAyamOutboundHttpClient NoOp = new NoOpAlAyamOutboundHttpClient();

    private sealed class NoOpAlAyamOutboundHttpClient : IAlAyamOutboundHttpClient
    {
        public Task<AlAyamOutboundHttpResponse?> GetAsync(Uri url, CancellationToken cancellationToken) =>
            Task.FromResult<AlAyamOutboundHttpResponse?>(null);
    }
}

public static class AlAyamPublisherHosts
{
    public static bool IsAlAyamHost(Uri uri) =>
        uri.Host.Contains("alayam.com", StringComparison.OrdinalIgnoreCase);
}
