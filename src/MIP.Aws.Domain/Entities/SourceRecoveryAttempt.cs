using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>AI recovery analysis and apply/retry lifecycle for a failed download.</summary>
public class SourceRecoveryAttempt : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public Guid? DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    /// <summary>Hangfire retry job created when an AI fix is applied.</summary>
    public Guid? RetryDownloadJobId { get; set; }

    public DownloadJob? RetryDownloadJob { get; set; }

    public string FailureType { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    /// <summary>Serialized <see cref="Application.Features.SourceRecovery.SourceRecoveryAnalysisDto"/>.</summary>
    public string AnalysisJson { get; set; } = "{}";

    public int SelectedOptionIndex { get; set; } = -1;

    public Guid? CandidateVersionId { get; set; }

    public SourceConfigurationVersion? CandidateVersion { get; set; }

    public Guid? RollbackVersionId { get; set; }

    public SourceConfigurationVersion? RollbackVersion { get; set; }

    public SourceRecoveryAttemptStatus Status { get; set; } = SourceRecoveryAttemptStatus.AnalysisGenerated;

    public Guid? AppliedByUserId { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ResultSummary { get; set; }

    public int? PredictedSuccessPercent { get; set; }

    public int? ActualSuccessPercent { get; set; }

    public bool IsAutomatic { get; set; }

    /// <summary>Parent auto-recovery run (no FK — avoids SQL Server cascade cycles).</summary>
    public Guid? AutoAiRecoveryRunId { get; set; }
}
