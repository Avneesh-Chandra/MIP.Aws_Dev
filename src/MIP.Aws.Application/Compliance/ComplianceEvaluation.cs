using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Compliance;

public sealed record ComplianceEvaluation(bool IsAllowed, string? ReasonCode, string? Detail);

public interface IPublisherComplianceGate
{
    Task<ComplianceEvaluation> EvaluateAsync(Uri resource, ContentAcquisitionMode mode, CancellationToken cancellationToken);
}
