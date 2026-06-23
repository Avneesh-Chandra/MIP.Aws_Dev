using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>
/// On worker startup, fail orphaned download jobs left Running by a previous container and heal stale rows.
/// </summary>
public sealed class DownloadJobWorkerStartupReconciliationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<HangfireHostOptions> hangfireHost,
    ILogger<DownloadJobWorkerStartupReconciliationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hangfireHost.Value.EnableJobServer)
        {
            return;
        }

        var workerStartedAt = DateTimeOffset.UtcNow;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var recoveryOrchestrator = scope.ServiceProvider.GetRequiredService<ISourceRecoveryOrchestrator>();
        var autoAiEnqueue = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryEnqueueService>();

        HangfireExpiredBatchJobCleanup.TryCancelExpiredOperatorBatchJobs(logger);

        await DownloadJobReconciliation.ReconcileWorkerRestartOrphansAsync(
                db,
                workerStartedAt,
                autoAiEnqueue,
                logger,
                cancellationToken)
            .ConfigureAwait(false);

        await DownloadJobReconciliation.ReconcileStaleJobsAsync(
                db,
                recoveryOrchestrator,
                autoAiEnqueue,
                logger,
                cancellationToken,
                requeueDownloads: false)
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
