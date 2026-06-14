using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface ISourceRecoveryAnalysisService
{
    Task<SourceRecoveryAnalysisDto> AnalyzeAndPersistAsync(
        Guid downloadJobId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<SourceRecoveryAnalysisDto?> GetAnalysisAsync(Guid attemptId, CancellationToken cancellationToken);
}
