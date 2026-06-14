using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Connectors;

/// <summary>
/// Pluggable connector that interprets a <see cref="NewsSource"/> configuration into concrete download work items.
/// </summary>
public interface INewsSourceConnector
{
    string ConnectorKey { get; }

    NewsSourceType SupportedType { get; }

    Task<IReadOnlyList<DownloadCandidate>> BuildDownloadPlanAsync(NewsSource source, CancellationToken cancellationToken);
}
