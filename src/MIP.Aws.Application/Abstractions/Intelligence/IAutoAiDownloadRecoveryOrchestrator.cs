using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAutoAiDownloadRecoveryOrchestrator
{
    Task<AutoAiRecoveryResultDto> RecoverAsync(
        Guid sourceId,
        Guid failedDownloadJobId,
        AutoAiRecoveryTrigger trigger,
        CancellationToken cancellationToken);
}
