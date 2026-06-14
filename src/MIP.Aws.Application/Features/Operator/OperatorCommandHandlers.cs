using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Operator;
using MediatR;

namespace MIP.Aws.Application.Features.Operator;

public sealed class GetDownloadMonitorQueryHandler(
    IOperatorDownloadMonitorService service,
    IAuditService audit) : IRequestHandler<GetDownloadMonitorQuery, DownloadMonitorDto>
{
    public async Task<DownloadMonitorDto> Handle(GetDownloadMonitorQuery request, CancellationToken cancellationToken)
    {
        var result = await service.GetMonitorAsync(request.MonitorDate, cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.DashboardViewed,
            "DownloadMonitor",
            result.MonitorDate.ToString("O"),
            new { result.Summary.TotalSources, result.Summary.FailedToday },
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}

public sealed class GetSourceDownloadStatusQueryHandler(IOperatorDownloadMonitorService service)
    : IRequestHandler<GetSourceDownloadStatusQuery, SourceDownloadStatusDto?>
{
    public Task<SourceDownloadStatusDto?> Handle(GetSourceDownloadStatusQuery request, CancellationToken cancellationToken) =>
        service.GetSourceStatusAsync(request.SourceId, request.MonitorDate, cancellationToken);
}

public sealed class GetOperatorLatestPdfQueryHandler(
    IOperatorDownloadMonitorService service,
    IAuditService audit) : IRequestHandler<GetOperatorLatestPdfQuery, LatestPdfLinkDto?>
{
    public async Task<LatestPdfLinkDto?> Handle(GetOperatorLatestPdfQuery request, CancellationToken cancellationToken)
    {
        var result = await service.GetLatestPdfLinkAsync(request.SourceId, cancellationToken).ConfigureAwait(false);
        if (result?.Available == true)
        {
            await audit.RecordAdminActionAsync(
                OperatorAuditEvents.LatestPdfViewed,
                "NewsSource",
                request.SourceId.ToString(),
                new { result.FileId },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

public sealed class GetDownloadFailureDetailsQueryHandler(
    IOperatorDownloadMonitorService service,
    IAuditService audit) : IRequestHandler<GetDownloadFailureDetailsQuery, DownloadFailureDetailsDto?>
{
    public async Task<DownloadFailureDetailsDto?> Handle(GetDownloadFailureDetailsQuery request, CancellationToken cancellationToken)
    {
        var result = await service.GetFailureDetailsAsync(request.DownloadJobId, cancellationToken).ConfigureAwait(false);
        if (result is not null)
        {
            await audit.RecordAdminActionAsync(
                OperatorAuditEvents.FailureDetailsViewed,
                "DownloadJob",
                request.DownloadJobId.ToString(),
                new { result.SourceId, result.FailureCode },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

public sealed class GetAiRecoverySuccessDetailsQueryHandler(
    IOperatorDownloadMonitorService service,
    IAuditService audit) : IRequestHandler<GetAiRecoverySuccessDetailsQuery, AiRecoverySuccessDetailsDto?>
{
    public async Task<AiRecoverySuccessDetailsDto?> Handle(
        GetAiRecoverySuccessDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAiRecoverySuccessDetailsAsync(request.RecoveryDownloadJobId, cancellationToken)
            .ConfigureAwait(false);
        if (result is not null)
        {
            await audit.RecordAdminActionAsync(
                OperatorAuditEvents.AiRecoveryDetailsViewed,
                "SourceRecoveryAttempt",
                result.AttemptId.ToString(),
                new { result.SourceId, request.RecoveryDownloadJobId },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

public sealed class GetAiRecoverySuccessDetailsByAttemptQueryHandler(
    IOperatorDownloadMonitorService service,
    IAuditService audit) : IRequestHandler<GetAiRecoverySuccessDetailsByAttemptQuery, AiRecoverySuccessDetailsDto?>
{
    public async Task<AiRecoverySuccessDetailsDto?> Handle(
        GetAiRecoverySuccessDetailsByAttemptQuery request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAiRecoverySuccessDetailsByAttemptAsync(request.AttemptId, cancellationToken)
            .ConfigureAwait(false);
        if (result is not null)
        {
            await audit.RecordAdminActionAsync(
                OperatorAuditEvents.AiRecoveryDetailsViewed,
                "SourceRecoveryAttempt",
                result.AttemptId.ToString(),
                new { result.SourceId, request.AttemptId },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

public sealed class AddDownloadOperatorNoteCommandHandler(
    IOperatorDownloadMonitorService service,
    ICurrentUserContext currentUser,
    IAuditService audit) : IRequestHandler<AddDownloadOperatorNoteCommand, Guid>
{
    public async Task<Guid> Handle(AddDownloadOperatorNoteCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        var noteId = await service.AddNoteAsync(request.DownloadJobId, request.Note, actorId, cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.NoteAdded,
            "DownloadJob",
            request.DownloadJobId.ToString(),
            new { noteId },
            cancellationToken).ConfigureAwait(false);
        return noteId;
    }
}

public sealed class InformAdminCommandHandler(
    IOperatorDownloadMonitorService service,
    ICurrentUserContext currentUser,
    IAuditService audit) : IRequestHandler<InformAdminCommand, Guid>
{
    public async Task<Guid> Handle(InformAdminCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        var notificationId = await service.InformAdminAsync(request.DownloadJobId, request.OperatorNote, actorId, cancellationToken)
            .ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.AdminInformed,
            "AdminInterventionNotification",
            notificationId.ToString(),
            new { request.DownloadJobId },
            cancellationToken).ConfigureAwait(false);
        return notificationId;
    }
}

public sealed class GetAdminInterventionNotificationsQueryHandler(IOperatorDownloadMonitorService service)
    : IRequestHandler<GetAdminInterventionNotificationsQuery, IReadOnlyList<AdminInterventionNotificationDto>>
{
    public Task<IReadOnlyList<AdminInterventionNotificationDto>> Handle(
        GetAdminInterventionNotificationsQuery request,
        CancellationToken cancellationToken) =>
        service.GetInterventionNotificationsAsync(request.PendingOnly, cancellationToken);
}

public sealed class AcknowledgeAdminInterventionCommandHandler(
    IOperatorDownloadMonitorService service,
    ICurrentUserContext currentUser,
    IAuditService audit) : IRequestHandler<AcknowledgeAdminInterventionCommand, Unit>
{
    public async Task<Unit> Handle(AcknowledgeAdminInterventionCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        await service.AcknowledgeInterventionAsync(request.NotificationId, adminId, cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.AdminAcknowledged,
            "AdminInterventionNotification",
            request.NotificationId.ToString(),
            null,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class ResolveAdminInterventionCommandHandler(
    IOperatorDownloadMonitorService service,
    ICurrentUserContext currentUser,
    IAuditService audit) : IRequestHandler<ResolveAdminInterventionCommand, Unit>
{
    public async Task<Unit> Handle(ResolveAdminInterventionCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        await service.ResolveInterventionAsync(request.NotificationId, adminId, cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.AdminResolved,
            "AdminInterventionNotification",
            request.NotificationId.ToString(),
            null,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
