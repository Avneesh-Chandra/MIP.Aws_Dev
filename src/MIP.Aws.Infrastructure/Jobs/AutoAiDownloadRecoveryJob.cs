using System.ComponentModel;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Domain.Enums;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Jobs;

[Queue("ai-recovery")]
public sealed class AutoAiDownloadRecoveryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoAiDownloadRecoveryJob> logger)
{
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [DisplayName("Auto AI Download Recovery — source {0}, job {1}")]
    public async Task RunAsync(
        Guid sourceId,
        Guid failedDownloadJobId,
        AutoAiRecoveryTrigger trigger,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryOrchestrator>();
        logger.LogInformation(
            "Starting auto AI download recovery for source {SourceId}, failed job {JobId}, trigger {Trigger}.",
            sourceId,
            failedDownloadJobId,
            trigger);

        await orchestrator.RecoverAsync(sourceId, failedDownloadJobId, trigger, cancellationToken).ConfigureAwait(false);
    }
}
