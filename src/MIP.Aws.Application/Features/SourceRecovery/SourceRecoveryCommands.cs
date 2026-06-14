using MediatR;

namespace MIP.Aws.Application.Features.SourceRecovery;

public sealed record AnalyzeSourceRecoveryCommand(Guid DownloadJobId) : IRequest<SourceRecoveryAnalysisDto>;

public sealed record GetSourceRecoveryAnalysisQuery(Guid AttemptId) : IRequest<SourceRecoveryAnalysisDto?>;

public sealed record PreviewSourceRecoveryCommand(Guid AttemptId, int OptionIndex) : IRequest<SourceRecoveryPreviewDto>;

public sealed record ApplySourceRecoveryCommand(Guid AttemptId, int OptionIndex) : IRequest<SourceRecoveryApplyResultDto>;

public sealed record FinalizeSourceRecoveryAttemptCommand(Guid AttemptId) : IRequest<SourceRecoveryApplyResultDto>;

public sealed record RollbackSourceRecoveryCommand(Guid AttemptId, string Reason) : IRequest<Unit>;

public sealed record GetSourceRecoveryHistoryQuery(int Take = 50, DateOnly? MonitorDate = null)
    : IRequest<IReadOnlyList<SourceRecoveryHistoryItemDto>>;

public sealed record GetRecoveryCenterFailuresQuery(DateOnly? MonitorDate = null)
    : IRequest<IReadOnlyList<SourceRecoveryCenterItemDto>>;

public sealed record SourceRecoveryCenterItemDto(
    Guid SourceId,
    string SourceName,
    Guid? DownloadJobId,
    string FailureType,
    string FailureMessage,
    DateTimeOffset? AttemptedAt,
    bool HasRecoveryAttempt);
