using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.Operator;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

internal static class DownloadMonitorInformAdminMailToBuilder
{
    public static async Task<string?> BuildForJobAsync(
        IApplicationDbContext db,
        IOperatorDownloadMonitorService monitorService,
        IMailSettingsService mailSettings,
        Guid downloadJobId,
        CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.Id == downloadJobId)
            .Select(j => new { j.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return null;
        }

        var monitorDate = DateOnly.FromDateTime(job.CreatedAt.UtcDateTime);
        var monitor = await monitorService
            .GetMonitorAsync(monitorDate, skipReconciliation: true, cancellationToken)
            .ConfigureAwait(false);
        var row = monitor.Sources.FirstOrDefault(s => s.LatestDownloadJobId == downloadJobId);
        if (row is null || !row.ManualInterventionRequired)
        {
            return null;
        }

        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        DownloadMonitorEmailFailureContext? context = null;
        var run = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.FailedDownloadJobId == downloadJobId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (run is not null)
        {
            var timeline = AutoAiRecoveryTimelineJson.Deserialize(run.TimelineJson)
                .Select(step => new DownloadMonitorEmailTimelineStep(step.Step, step.Detail, step.Timestamp))
                .ToList();
            context = new DownloadMonitorEmailFailureContext(run.ResultSummary, timeline);
        }

        return DownloadMonitorStatusEmailActionHelper.BuildInformAdminMailTo(
            scheduler.AdminRecipientEmail,
            row,
            monitor.MonitorDate,
            context);
    }
}
