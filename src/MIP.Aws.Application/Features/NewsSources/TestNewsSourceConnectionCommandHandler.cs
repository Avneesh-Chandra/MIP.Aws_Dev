using System.Net.Http;
using MIP.Aws.Application.Abstractions.Crawling;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourceConnectionCommandHandler(IRobotsPolicyService robots, IHttpClientFactory httpClientFactory)
    : IRequestHandler<TestNewsSourceConnectionCommand, NewsSourceConnectionTestResult>
{
    public async Task<NewsSourceConnectionTestResult> Handle(TestNewsSourceConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.BaseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return new NewsSourceConnectionTestResult(false, "Invalid URL.", null);
        }

        var robotsOk = await robots.IsAllowedAsync(uri, request.AcquisitionMode, cancellationToken).ConfigureAwait(false);
        if (!robotsOk)
        {
            return new NewsSourceConnectionTestResult(false, "robots.txt / compliance gate denied this URL for the selected acquisition mode.", null);
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MIP.Aws/1.0 (source-test; +https://www.gfh.com)");
        using var head = new HttpRequestMessage(HttpMethod.Head, uri);
        using var response = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return new NewsSourceConnectionTestResult(true, "HEAD request succeeded and robots policy allows the URL.", (int)response.StatusCode);
        }

        using var get = new HttpRequestMessage(HttpMethod.Get, uri);
        using var getResp = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        return getResp.IsSuccessStatusCode
            ? new NewsSourceConnectionTestResult(true, "GET request succeeded (HEAD was not accepted).", (int)getResp.StatusCode)
            : new NewsSourceConnectionTestResult(false, $"Host responded with HTTP {(int)getResp.StatusCode}.", (int)getResp.StatusCode);
    }
}
