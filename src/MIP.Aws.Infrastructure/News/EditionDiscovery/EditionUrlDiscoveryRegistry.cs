using MIP.Aws.Application.Abstractions.News;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

public sealed class EditionUrlDiscoveryRegistry(IEnumerable<IEditionUrlDiscovery> discoveries)
{
    private readonly IReadOnlyDictionary<string, IEditionUrlDiscovery> _byKey =
        discoveries.ToDictionary(d => d.ConnectorKey, StringComparer.OrdinalIgnoreCase);

    public bool Contains(string? connectorKey) =>
        !string.IsNullOrWhiteSpace(connectorKey) && _byKey.ContainsKey(connectorKey);

    public IEditionUrlDiscovery GetRequired(string connectorKey) =>
        _byKey.TryGetValue(connectorKey, out var discovery)
            ? discovery
            : throw new InvalidOperationException($"No edition discovery registered for connector key '{connectorKey}'.");
}
