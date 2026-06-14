using MIP.Aws.Application.Connectors;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Connectors;

public sealed class RssNewsSourceConnector : INewsSourceConnector, IRssNewsConnector
{
    public string ConnectorKey => "default.rss";

    public NewsSourceType SupportedType => NewsSourceType.Rss;

    public Task<IReadOnlyList<DownloadCandidate>> BuildDownloadPlanAsync(NewsSource source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri))
        {
            return Task.FromResult<IReadOnlyList<DownloadCandidate>>(Array.Empty<DownloadCandidate>());
        }

        IReadOnlyList<DownloadCandidate> plan = [new DownloadCandidate(uri, "application/rss+xml", source.Name)];
        return Task.FromResult(plan);
    }
}
