using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.Crawling;

/// <summary>
/// Evaluates robots.txt rules for a crawler user-agent before issuing fetches.
/// </summary>
public interface IRobotsPolicyService
{
    /// <summary>
    /// Returns true when the resource may be fetched under the configured acquisition mode.
    /// </summary>
    Task<bool> IsAllowedAsync(Uri resource, ContentAcquisitionMode acquisitionMode, CancellationToken cancellationToken);
}
