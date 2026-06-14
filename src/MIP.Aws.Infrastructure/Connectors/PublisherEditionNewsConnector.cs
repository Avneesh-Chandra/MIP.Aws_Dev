using MIP.Aws.Application.Connectors;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.EditionDiscovery;

namespace MIP.Aws.Infrastructure.Connectors;

/// <summary>
/// Resolves the latest edition URL via <see cref="EditionUrlDiscoveryRegistry"/> then downloads it.
/// </summary>
public sealed class PublisherEditionNewsConnector(EditionUrlDiscoveryRegistry registry) : INewsSourceConnector
{
    public string ConnectorKey => "news.publisher-edition";

    public NewsSourceType SupportedType => NewsSourceType.PublicPdf;

    public async Task<IReadOnlyList<DownloadCandidate>> BuildDownloadPlanAsync(NewsSource source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.ConnectorKey))
        {
            return Array.Empty<DownloadCandidate>();
        }

        var discovery = registry.GetRequired(source.ConnectorKey);
        var edition = await discovery.DiscoverLatestEditionAsync(source, cancellationToken).ConfigureAwait(false);
        return [new DownloadCandidate(edition.ResourceUri, edition.ContentTypeHint, edition.Title ?? source.Name)];
    }
}
