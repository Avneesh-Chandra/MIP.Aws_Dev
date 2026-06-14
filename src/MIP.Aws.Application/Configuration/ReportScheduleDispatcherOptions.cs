namespace MIP.Aws.Application.Configuration;

/// <summary>Hangfire cadence for scanning due report schedules.</summary>
public sealed class ReportScheduleDispatcherOptions
{
    public const string SectionName = "ReportScheduleDispatcher";

    /// <summary>Cron expression (default: every 5 minutes).</summary>
    public string CronExpression { get; set; } = "*/5 * * * *";

    public int BatchSize { get; set; } = 20;

    /// <summary>Cron for retrying failed email logs.</summary>
    public string RetryCronExpression { get; set; } = "*/15 * * * *";
}
