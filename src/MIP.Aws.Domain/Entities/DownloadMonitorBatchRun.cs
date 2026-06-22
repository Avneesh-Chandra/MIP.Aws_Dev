using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// PDF download batch metadata (scheduled daily or operator-triggered) shared across API instances.
/// </summary>
public sealed class DownloadMonitorBatchRun : AuditableEntity
{
    public DateTimeOffset StartedAt { get; set; }

    public int TotalSources { get; set; }

    public string HangfireJobId { get; set; } = string.Empty;
}
