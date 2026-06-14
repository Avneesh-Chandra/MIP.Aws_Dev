using MIP.Aws.Application.Connectors;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.News.EditionDiscovery;

namespace MIP.Aws.Infrastructure.Connectors;

public sealed class NewsSourceConnectorFactory(
    IEnumerable<INewsSourceConnector> connectors,
    EditionUrlDiscoveryRegistry editionRegistry,
    PublisherEditionNewsConnector publisherEditionConnector) : INewsSourceConnectorFactory
{
    private readonly IReadOnlyList<INewsSourceConnector> _connectors = connectors.ToList();

    public INewsSourceConnector Resolve(NewsSource source)
    {
        if (source.SourceType is NewsSourceType.WebPortalLogin or NewsSourceType.ManualUpload)
        {
            throw new InvalidOperationException("Connector resolution is not used for WebPortalLogin or ManualUpload sources; use the portal automation or manual ingestion path.");
        }

        if (!string.IsNullOrWhiteSpace(source.ConnectorKey) && editionRegistry.Contains(source.ConnectorKey))
        {
            return publisherEditionConnector;
        }

        if (!string.IsNullOrWhiteSpace(source.ConnectorKey))
        {
            var match = _connectors.FirstOrDefault(c =>
                string.Equals(c.ConnectorKey, source.ConnectorKey, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var typeMatch = _connectors.FirstOrDefault(c => c.SupportedType == source.SourceType);
        return typeMatch ?? _connectors.First(c => c.ConnectorKey == "default.html");
    }
}
