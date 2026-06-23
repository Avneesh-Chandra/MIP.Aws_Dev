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

    /// <summary>When the post-batch download monitor status email was sent (null = not yet sent).</summary>
    public DateTimeOffset? StatusEmailSentAt { get; set; }

    /// <summary>When the operator aborts a batch, progress UI stops immediately instead of waiting for the stagger window.</summary>
    public DateTimeOffset? AbortedAt { get; set; }
}
