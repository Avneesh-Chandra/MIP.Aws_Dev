using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// Resolves the latest downloadable edition URL for a configured newspaper source.
/// </summary>
public interface IEditionUrlDiscovery
{
    string ConnectorKey { get; }

    Task<EditionDiscoveryResult> DiscoverLatestEditionAsync(NewsSource source, CancellationToken cancellationToken);
}

public sealed record EditionDiscoveryResult(Uri ResourceUri, string ContentTypeHint, string? Title);
