using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.SourceRecovery;

public sealed record SelectorRecoveryCandidateDto(
    string Selector,
    string Source,
    int Confidence,
    SelectorRecoveryStrategy Strategy,
    string? FieldName);

public sealed record SourceRecoveryOptionDto(
    int OptionIndex,
    string Title,
    string Description,
    string ExpectedFix,
    int ConfidenceScore,
    int PredictedSuccessPercent,
    SourceRecoveryRiskLevel RiskLevel,
    IReadOnlyList<string> AffectedFields,
    IReadOnlyList<string> AffectedWorkflowSteps,
    SourceRecoveryConfigurationPatchDto Patch,
    IReadOnlyList<SelectorRecoveryCandidateDto> SelectorCandidates);

public sealed record SourceRecoveryConfigurationPatchDto(
    string? UsernameSelector,
    string? PasswordSelector,
    string? SubmitSelector,
    string? DownloadSelector,
    string? LoginIconSelector,
    string? NewspaperCanvasSelector,
    string? ContextMenuSelector,
    string? DownloadMenuItemSelector,
    string? LoginSuccessSelector,
    string? SuccessUrlPattern,
    string? PdfDownloadSelector,
    string? PdfLinkSelector,
    string? BaseUrl,
    string? EditionUrl,
    string? PdfDiscoveryPageUrl,
    int? DownloadWaitTimeoutSeconds,
    bool? UseHeadlessBrowser = null);

public sealed record SourceRecoveryAnalysisDto(
    Guid AttemptId,
    Guid SourceId,
    string SourceName,
    Guid? DownloadJobId,
    string FailureType,
    string FailureMessage,
    string? SourceUrl,
    DateTimeOffset? AttemptedAt,
    int RetryCount,
    string? ScreenshotUrl,
    string? HtmlSnapshotUrl,
    IReadOnlyList<SourceRecoveryOptionDto> Options,
    int? RecommendedOptionIndex,
    IReadOnlyList<string> ScreenshotFindings,
    IReadOnlyList<string> HtmlFindings,
    bool AiEnabled);

public sealed record SourceRecoveryHistoryItemDto(
    Guid AttemptId,
    Guid SourceId,
    string SourceName,
    Guid? DownloadJobId,
    Guid? RetryDownloadJobId,
    string FailureType,
    string? SelectedOptionTitle,
    string AppliedByLabel,
    SourceRecoveryAttemptStatus Status,
    string? ResultSummary,
    int? PredictedSuccessPercent,
    int? ActualSuccessPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record SourceRecoveryApplyResultDto(
    Guid AttemptId,
    Guid CandidateVersionId,
    Guid? RollbackVersionId,
    SourceRecoveryAttemptStatus Status,
    string Message);

public sealed record SourceRecoveryPreviewDto(
    SourceRecoveryConfigurationPatchDto Patch,
    IReadOnlyList<(string Field, string? CurrentValue, string? NewValue)> Changes);
