using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface ISourceRecoveryOrchestrator
{
    Task<SourceRecoveryPreviewDto> PreviewChangesAsync(
        Guid attemptId,
        int optionIndex,
        CancellationToken cancellationToken);

    Task<SourceRecoveryApplyResultDto> ApplyAndRetryAsync(
        Guid attemptId,
        int optionIndex,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<SourceRecoveryApplyResultDto> FinalizeAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken);

    /// <summary>Completes recovery attempts whose retry job finished but finalize never persisted.</summary>
    Task ReconcileUnfinalizedAttemptsAsync(CancellationToken cancellationToken);

    Task RollbackAsync(Guid attemptId, Guid actorUserId, string reason, CancellationToken cancellationToken);
}
