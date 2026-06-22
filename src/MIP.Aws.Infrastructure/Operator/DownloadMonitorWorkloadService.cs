using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorWorkloadService(
    IServiceScopeFactory scopeFactory,
    IDownloadMonitorBatchRunService batchRunService,
    ILogger<DownloadMonitorWorkloadService> logger) : IDownloadMonitorWorkloadService
{
    public async Task<DownloadMonitorWorkloadSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var batchProgress = await batchRunService
            .GetProgressAsync(null, skipReconciliation: true, cancellationToken)
            .ConfigureAwait(false);

        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        var sourceIds = sources.Select(s => s.Id).ToHashSet();
        var sourceNames = sources.ToDictionary(s => s.Id, s => s.Name);
        var dayStart = DateTimeOffset.UtcNow.Date;

        var activeJobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && sourceIds.Contains(j.NewsSourceId)
                        && j.CreatedAt >= dayStart
                        && (j.Status == DownloadJobStatus.Pending
                            || j.Status == DownloadJobStatus.Running
                            || j.Status == DownloadJobStatus.AutoAiRecoveryAnalyzing
                            || j.Status == DownloadJobStatus.AutoAiRecoveryApplying
                            || j.Status == DownloadJobStatus.AutoAiRecoveryRetrying))
            .OrderBy(j => j.CreatedAt)
            .Select(j => new
            {
                j.Id,
                j.NewsSourceId,
                j.Status,
                j.CreatedAt,
                j.StartedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var jobResults = activeJobs
            .Select(j =>
            {
                var (status, activity) = DescribeActiveJob(j.Status);
                sourceNames.TryGetValue(j.NewsSourceId, out var name);
                return new DownloadMonitorActiveJobResult(
                    j.Id,
                    j.NewsSourceId,
                    name ?? "Unknown source",
                    status,
                    activity,
                    j.CreatedAt,
                    j.StartedAt);
            })
            .ToList();

        var pendingHangfire = sourceIds.Count > 0
            ? HangfireOperatorDownloadJobCleanup.CountMonitoredSourceDownloadJobs(sourceIds)
            : 0;

        var hasActiveWork = batchProgress?.IsActive == true
                            || jobResults.Count > 0
                            || pendingHangfire > 0;

        return new DownloadMonitorWorkloadSnapshot(batchProgress, jobResults, pendingHangfire, hasActiveWork);
    }

    public async Task<AbortDownloadMonitorWorkResult> AbortActiveWorkAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var recoveryOrchestrator = scope.ServiceProvider.GetRequiredService<ISourceRecoveryOrchestrator>();
        var autoAiEnqueue = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryEnqueueService>();

        var batchProgress = await batchRunService
            .GetProgressAsync(null, skipReconciliation: true, cancellationToken)
            .ConfigureAwait(false);

        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        var sourceIds = sources.Select(s => s.Id).ToHashSet();
        var dayStart = DateTimeOffset.UtcNow.Date;

        var orchestratorStopped = HangfireOperatorDownloadJobCleanup.TryDeleteOperatorBatchOrchestrator(
            batchProgress?.HangfireJobId,
            logger);

        var hangfireRemoved = HangfireOperatorDownloadJobCleanup.TryRemoveMonitoredSourceDownloadJobs(sourceIds, logger);

        var activeJobs = await db.DownloadJobs
            .Where(j => !j.IsDeleted
                        && sourceIds.Contains(j.NewsSourceId)
                        && j.CreatedAt >= dayStart
                        && (j.Status == DownloadJobStatus.Pending
                            || j.Status == DownloadJobStatus.Running
                            || j.Status == DownloadJobStatus.AutoAiRecoveryAnalyzing
                            || j.Status == DownloadJobStatus.AutoAiRecoveryApplying
                            || j.Status == DownloadJobStatus.AutoAiRecoveryRetrying))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var job in activeJobs)
        {
            job.Status = DownloadJobStatus.Cancelled;
            job.ErrorMessage = "Cancelled by operator.";
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ModifiedAt = DateTimeOffset.UtcNow;
        }

        if (activeJobs.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await DownloadJobReconciliation.ReconcileStaleJobsAsync(
                db,
                recoveryOrchestrator,
                autoAiEnqueue,
                logger,
                cancellationToken,
                requeueDownloads: false)
            .ConfigureAwait(false);

        HangfireExpiredBatchJobCleanup.TryCancelExpiredOperatorBatchJobs(logger);

        var summary = $"Cancelled {activeJobs.Count} download job(s), removed {hangfireRemoved} queued Hangfire job(s)"
                      + (orchestratorStopped ? ", and stopped the batch orchestrator." : ".");

        logger.LogWarning("Operator aborted active download monitor work. {Summary}", summary);

        return new AbortDownloadMonitorWorkResult(
            activeJobs.Count,
            hangfireRemoved,
            orchestratorStopped,
            summary);
    }

    private static (string Status, string Activity) DescribeActiveJob(DownloadJobStatus status) => status switch
    {
        DownloadJobStatus.Pending => ("Waiting", "Queued for download"),
        DownloadJobStatus.Running => ("InProgress", "Downloading"),
        DownloadJobStatus.AutoAiRecoveryAnalyzing => ("AutoRecovery", "Analyzing failure with AI"),
        DownloadJobStatus.AutoAiRecoveryApplying => ("AutoRecovery", "Applying AI-suggested fix"),
        DownloadJobStatus.AutoAiRecoveryRetrying => ("AutoRecovery", "Retrying after AI fix"),
        _ => ("InProgress", "Processing")
    };

    private static async Task<List<Domain.Entities.NewsSource>> LoadMonitoredSourcesAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var all = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return all.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).ToList();
    }
}
