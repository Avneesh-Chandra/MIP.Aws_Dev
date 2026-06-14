using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>Tracks an automatic AI download recovery session for a failed download job.</summary>
public sealed class AutoAiRecoveryRun : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public Guid FailedDownloadJobId { get; set; }

    public DownloadJob FailedDownloadJob { get; set; } = null!;

    public AutoAiRecoveryTrigger Trigger { get; set; }

    public AutoAiRecoveryRunStatus Status { get; set; } = AutoAiRecoveryRunStatus.Queued;

    public Guid? SourceRecoveryAttemptId { get; set; }

    public SourceRecoveryAttempt? SourceRecoveryAttempt { get; set; }

    public Guid? RetryDownloadJobId { get; set; }

    public Guid? SuccessfulCandidateVersionId { get; set; }

    public int SuggestionsTried { get; set; }

    public int? SuccessfulOptionIndex { get; set; }

    public string? SuccessfulOptionTitle { get; set; }

    /// <summary>JSON array of timeline steps (step, timestamp, detail).</summary>
    public string TimelineJson { get; set; } = "[]";

    public string? ResultSummary { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
