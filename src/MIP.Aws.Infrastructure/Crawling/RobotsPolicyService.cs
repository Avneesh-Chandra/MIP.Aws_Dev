using MIP.Aws.Application.Abstractions.Crawling;
using MIP.Aws.Application.Compliance;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Crawling;

/// <summary>
/// Delegates crawl policy to the publisher compliance gate (robots-aware for public web).
/// </summary>
public sealed class RobotsPolicyService(IPublisherComplianceGate complianceGate) : IRobotsPolicyService
{
    public async Task<bool> IsAllowedAsync(Uri resource, ContentAcquisitionMode acquisitionMode, CancellationToken cancellationToken)
    {
        var evaluation = await complianceGate.EvaluateAsync(resource, acquisitionMode, cancellationToken).ConfigureAwait(false);
        return evaluation.IsAllowed;
    }
}
