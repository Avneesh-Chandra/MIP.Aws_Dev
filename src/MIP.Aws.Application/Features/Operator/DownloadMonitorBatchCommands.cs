using MIP.Aws.Application.Abstractions.Operator;
using MediatR;

namespace MIP.Aws.Application.Features.Operator;

public sealed record ExecuteDownloadMonitorBatchCommand(Guid ActorUserId)
    : IRequest<DownloadMonitorBatchRunResult>;

public sealed record GetDownloadMonitorBatchProgressQuery(DateTimeOffset? BatchStartedAt = null)
    : IRequest<DownloadMonitorBatchProgressResult?>;
