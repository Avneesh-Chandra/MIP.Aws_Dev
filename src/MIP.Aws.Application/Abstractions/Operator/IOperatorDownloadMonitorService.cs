using MIP.Aws.Application.Features.Operator;

namespace MIP.Aws.Application.Abstractions.Operator;

public interface IOperatorDownloadMonitorService
{
    Task<DownloadMonitorDto> GetMonitorAsync(DateOnly? monitorDate, CancellationToken cancellationToken);

    Task<SourceDownloadStatusDto?> GetSourceStatusAsync(Guid sourceId, DateOnly? monitorDate, CancellationToken cancellationToken);

    Task<LatestPdfLinkDto?> GetLatestPdfLinkAsync(Guid sourceId, CancellationToken cancellationToken);

    Task<DownloadFailureDetailsDto?> GetFailureDetailsAsync(Guid downloadJobId, CancellationToken cancellationToken);

    Task<AiRecoverySuccessDetailsDto?> GetAiRecoverySuccessDetailsAsync(
        Guid recoveryDownloadJobId,
        CancellationToken cancellationToken);

    Task<AiRecoverySuccessDetailsDto?> GetAiRecoverySuccessDetailsByAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<Guid> AddNoteAsync(Guid downloadJobId, string note, Guid actorUserId, CancellationToken cancellationToken);

    Task<Guid> InformAdminAsync(Guid downloadJobId, string? operatorNote, Guid actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminInterventionNotificationDto>> GetInterventionNotificationsAsync(
        bool pendingOnly,
        CancellationToken cancellationToken);

    Task AcknowledgeInterventionAsync(Guid notificationId, Guid adminUserId, CancellationToken cancellationToken);

    Task ResolveInterventionAsync(Guid notificationId, Guid adminUserId, CancellationToken cancellationToken);
}
