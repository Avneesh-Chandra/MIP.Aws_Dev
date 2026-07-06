using MediatR;

namespace MIP.Aws.Application.Features.Operator;

public sealed record GetDownloadMonitorQuery(DateOnly? MonitorDate = null, bool SkipReconciliation = true)
    : IRequest<DownloadMonitorDto>;

/// <summary>Runs a single bounded reconciliation pass before monitor reads (avoid repeating per query).</summary>
public sealed record ReconcileDownloadMonitorStateCommand(TimeSpan? MaxDuration = null) : IRequest;

public sealed record GetSourceDownloadStatusQuery(Guid SourceId, DateOnly? MonitorDate = null) : IRequest<SourceDownloadStatusDto?>;

public sealed record GetOperatorLatestPdfQuery(Guid SourceId) : IRequest<LatestPdfLinkDto?>;

public sealed record GetDownloadFailureDetailsQuery(Guid DownloadJobId) : IRequest<DownloadFailureDetailsDto?>;

public sealed record GetAiRecoverySuccessDetailsQuery(Guid RecoveryDownloadJobId)
    : IRequest<AiRecoverySuccessDetailsDto?>;

public sealed record GetAiRecoverySuccessDetailsByAttemptQuery(Guid AttemptId)
    : IRequest<AiRecoverySuccessDetailsDto?>;

public sealed record AddDownloadOperatorNoteCommand(Guid DownloadJobId, string Note) : IRequest<Guid>;

public sealed record InformAdminCommand(Guid DownloadJobId, string? OperatorNote) : IRequest<Guid>;

public sealed record GetInformAdminMailToQuery(Guid DownloadJobId) : IRequest<string?>;

public sealed record GetAdminInterventionNotificationsQuery(bool PendingOnly = false)
    : IRequest<IReadOnlyList<AdminInterventionNotificationDto>>;

public sealed record AcknowledgeAdminInterventionCommand(Guid NotificationId) : IRequest<Unit>;

public sealed record ResolveAdminInterventionCommand(Guid NotificationId) : IRequest<Unit>;
