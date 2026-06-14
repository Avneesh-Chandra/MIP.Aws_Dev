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
        service.GetProgressAsync(request.BatchStartedAt, cancellationToken);
}
