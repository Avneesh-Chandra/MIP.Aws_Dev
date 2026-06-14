using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAutoAiDownloadRecoverySettingsReader
{
    Task<AutoAiDownloadRecoveryOptions> GetEffectiveAsync(CancellationToken cancellationToken);
}
