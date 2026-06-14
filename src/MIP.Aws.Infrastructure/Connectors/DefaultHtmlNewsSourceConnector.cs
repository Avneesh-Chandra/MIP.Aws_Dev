using MIP.Aws.Application.Connectors;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Connectors;

public sealed class DefaultHtmlNewsSourceConnector : INewsSourceConnector, IHtmlNewsConnector
{
    public string ConnectorKey => "default.html";

    public NewsSourceType SupportedType => NewsSourceType.PublicHtml;

    public Task<IReadOnlyList<DownloadCandidate>> BuildDownloadPlanAsync(NewsSource source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri))
        {
            return Task.FromResult<IReadOnlyList<DownloadCandidate>>(Array.Empty<DownloadCandidate>());
        }

        IReadOnlyList<DownloadCandidate> plan = [new DownloadCandidate(uri, "text/html", source.Name)];
        return Task.FromResult(plan);
    }
}
