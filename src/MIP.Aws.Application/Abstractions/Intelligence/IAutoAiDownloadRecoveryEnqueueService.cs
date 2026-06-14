using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAutoAiDownloadRecoveryEnqueueService
{
    Task TryEnqueueAfterFailureAsync(DownloadJob failedJob, CancellationToken cancellationToken);
}
