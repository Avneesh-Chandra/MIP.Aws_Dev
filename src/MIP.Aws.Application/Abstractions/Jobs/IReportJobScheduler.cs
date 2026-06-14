namespace MIP.Aws.Application.Abstractions.Jobs;

/// <summary>Enqueues Hangfire report-distribution work without referencing Hangfire from Application.</summary>
public interface IReportJobScheduler
{
    /// <summary>Runs <c>reporting-scheduled-generate</c> (processes all due report schedules).</summary>
    void EnqueueScheduledReportGeneration();
}
