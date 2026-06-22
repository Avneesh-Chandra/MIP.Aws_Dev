using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Operator;
using MediatR;

namespace MIP.Aws.Application.Features.Operator;

public sealed class ExecuteDownloadMonitorBatchCommandHandler(
    IDownloadMonitorBatchRunService service,
    IAuditService audit)
    : IRequestHandler<ExecuteDownloadMonitorBatchCommand, DownloadMonitorBatchRunResult>
{
    public async Task<DownloadMonitorBatchRunResult> Handle(
        ExecuteDownloadMonitorBatchCommand request,
        CancellationToken cancellationToken)
    {
        var result = await service.StartBatchAsync(cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.BatchExecutionStarted,
            "DownloadMonitor",
            result.StartedAt.ToString("O"),
            new { result.TotalSources, result.HangfireJobId, request.ActorUserId },
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}

public sealed class GetDownloadMonitorBatchProgressQueryHandler(IDownloadMonitorBatchRunService service)
    : IRequestHandler<GetDownloadMonitorBatchProgressQuery, DownloadMonitorBatchProgressResult?>
{
    public Task<DownloadMonitorBatchProgressResult?> Handle(
        GetDownloadMonitorBatchProgressQuery request,
        CancellationToken cancellationToken) =>
        service.GetProgressAsync(request.BatchStartedAt, request.SkipReconciliation, cancellationToken);
}

public sealed class GetDownloadMonitorWorkloadQueryHandler(IDownloadMonitorWorkloadService service)
    : IRequestHandler<GetDownloadMonitorWorkloadQuery, DownloadMonitorWorkloadSnapshot>
{
    public Task<DownloadMonitorWorkloadSnapshot> Handle(
        GetDownloadMonitorWorkloadQuery request,
        CancellationToken cancellationToken) =>
        service.GetSnapshotAsync(cancellationToken);
}

public sealed class AbortDownloadMonitorWorkCommandHandler(
    IDownloadMonitorWorkloadService service,
    IAuditService audit)
    : IRequestHandler<AbortDownloadMonitorWorkCommand, AbortDownloadMonitorWorkResult>
{
    public async Task<AbortDownloadMonitorWorkResult> Handle(
        AbortDownloadMonitorWorkCommand request,
        CancellationToken cancellationToken)
    {
        var result = await service.AbortActiveWorkAsync(cancellationToken).ConfigureAwait(false);
        await audit.RecordAdminActionAsync(
            OperatorAuditEvents.BatchWorkAborted,
            "DownloadMonitor",
            null,
            new
            {
                request.ActorUserId,
                result.DownloadJobsCancelled,
                result.HangfireJobsRemoved,
                result.BatchOrchestratorStopped
            },
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}
