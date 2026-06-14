using MIP.Aws.Domain.Enums;
using MediatR;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public sealed record TriggerAutoAiRecoveryCommand(Guid DownloadJobId, Guid ActorUserId)
    : IRequest<AutoAiRecoveryResultDto>;

public sealed record GetAutoAiRecoveryStatusQuery(Guid DownloadJobId)
    : IRequest<AutoAiRecoveryStatusDto?>;

public sealed record GetAutoAiRecoveryTimelineQuery(Guid DownloadJobId)
    : IRequest<IReadOnlyList<AutoAiRecoveryTimelineStepDto>>;

public sealed record GetAutoAiDownloadRecoverySettingsQuery()
    : IRequest<AutoAiDownloadRecoverySettingsDto>;

public sealed record UpdateAutoAiDownloadRecoverySettingsCommand(AutoAiDownloadRecoverySettingsDto Settings, Guid ActorUserId)
    : IRequest<AutoAiDownloadRecoverySettingsDto>;

public sealed record SetSourceAutoAiRecoveryCommand(Guid SourceId, bool Enabled, Guid ActorUserId)
    : IRequest<bool>;
