namespace MIP.Aws.Application.Features.Operator;

public sealed record DownloadMonitorSummaryDto(
    int TotalSources,
    int SuccessfulToday,
    int FailedToday,
    int PendingManualIntervention,
    int PdfsDownloadedToday,
    int EditionDateMismatchCount,
    int AdminNotificationsPending,
    IReadOnlyList<AttentionSourceDto> SourcesRequiringAttention);

public sealed record AttentionSourceDto(
    Guid SourceId,
    string SourceName,
    string Issue,
    string ActionRequired);

public sealed record DownloadMonitorSourceRowDto(
    Guid SourceId,
    string SourceName,
    string SourceType,
    string? Country,
    string? Language,
    string LastDownloadStatus,
    DateTimeOffset? LastDownloadTime,
    DateTimeOffset? LastSuccessfulDownload,
    DateTimeOffset? LastFailedDownload,
    Guid? LatestPdfFileId,
    Guid? LatestDownloadJobId,
    string? FailureReason,
    string? FailureCode,
    bool ManualInterventionRequired,
    bool AdminInformed,
    bool InformAdminDisabled,
    string? SuggestedIntervention,
    Guid? AiRecoveryAttemptId = null,
    DateOnly? LatestPdfEditionDate = null,
    bool EditionDateMatchesMonitor = true);

public sealed record AiRecoveryConfigurationChangeDto(
    string Field,
    string? BeforeValue,
    string? AfterValue);

public sealed record AiRecoverySuccessDetailsDto(
    Guid AttemptId,
    Guid SourceId,
    string SourceName,
    Guid? OriginalDownloadJobId,
    Guid? RecoveryDownloadJobId,
    string FailureType,
    string? FailureCode,
    string OriginalFailureMessage,
    DateTimeOffset? OriginalFailedAt,
    string? ScreenshotUrl,
    string? HtmlSnapshotUrl,
    string AppliedOptionTitle,
    string AppliedOptionDescription,
    string? AppliedOptionExpectedFix,
    int? PredictedSuccessPercent,
    int? ActualSuccessPercent,
    string AppliedByLabel,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? RecoveryCompletedAt,
    string OutcomeSummary,
    IReadOnlyList<AiRecoveryConfigurationChangeDto> ConfigurationChanges,
    bool IsAutomatic = false);

public sealed record DownloadMonitorDto(
    DateOnly MonitorDate,
    DownloadMonitorSummaryDto Summary,
    IReadOnlyList<DownloadMonitorSourceRowDto> Sources);

public sealed record SourceDownloadStatusDto(
    Guid SourceId,
    string SourceName,
    string LastDownloadStatus,
    DateTimeOffset? LastDownloadTime,
    DateTimeOffset? LastSuccessfulDownload,
    DateTimeOffset? LastFailedDownload,
    string? FailureReason,
    string? FailureCode,
    bool ManualInterventionRequired,
    bool AdminInformed,
    string? SuggestedIntervention,
    IReadOnlyList<DownloadAttemptTimelineDto> RecentAttempts);

public sealed record DownloadAttemptTimelineDto(
    Guid? DownloadJobId,
    string Status,
    DateTimeOffset? AttemptedAt,
    string? FailureReason,
    string? FailureCode);

public sealed record LatestPdfLinkDto(
    Guid SourceId,
    Guid? FileId,
    Guid? DownloadJobId,
    string? ViewUrl,
    string? DownloadUrl,
    bool Available,
    DateTimeOffset? DownloadedAt);

public sealed record DownloadFailureDetailsDto(
    Guid SourceId,
    string SourceName,
    Guid? DownloadJobId,
    string? FailureCode,
    string FailureMessage,
    string? SourceUrl,
    DateTimeOffset? AttemptedAt,
    string? ScreenshotUrl,
    string? HtmlSnapshotUrl,
    int RetryCount,
    string SuggestedIntervention,
    bool ComplianceBlocked,
    bool RequiresManualAction,
    LatestPdfLinkDto? LastSuccessfulPdf,
    IReadOnlyList<DownloadOperatorNoteDto> OperatorNotes);

public sealed record DownloadOperatorNoteDto(
    Guid Id,
    string Note,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record AdminInterventionNotificationDto(
    Guid Id,
    Guid SourceId,
    string SourceName,
    Guid? DownloadJobId,
    string FailureReason,
    string? FailureCode,
    string SuggestedAction,
    string? OperatorNote,
    string Status,
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt);
