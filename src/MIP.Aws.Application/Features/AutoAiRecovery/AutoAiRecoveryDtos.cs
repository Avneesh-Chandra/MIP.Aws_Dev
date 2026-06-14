using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public sealed record AutoAiRecoveryTimelineStepDto(
    int Order,
    string Step,
    DateTimeOffset Timestamp,
    string Detail,
    bool Succeeded);

public sealed record AutoAiRecoveryResultDto(
    Guid RunId,
    Guid SourceId,
    Guid FailedDownloadJobId,
    AutoAiRecoveryRunStatus Status,
    bool Succeeded,
    string Summary,
    int SuggestionsTried,
    string? SuccessfulOptionTitle,
    Guid? RetryDownloadJobId,
    IReadOnlyList<AutoAiRecoveryTimelineStepDto> Timeline);

public sealed record AutoAiRecoveryStatusDto(
    Guid RunId,
    Guid FailedDownloadJobId,
    AutoAiRecoveryRunStatus Status,
    string? ResultSummary,
    int SuggestionsTried,
    string? SuccessfulOptionTitle,
    Guid? RetryDownloadJobId,
    DateTimeOffset? CompletedAt);

public sealed record AutoAiDownloadRecoverySettingsDto(
    bool Enabled,
    bool RunAfterScheduledFailure,
    bool RunAfterManualFailure,
    int MaxSuggestionsToTry,
    double MinimumConfidence,
    string MaximumRiskAllowed,
    bool RequireHumanApprovalForMediumRisk,
    int CooldownMinutesPerSource,
    int MaxAutoRecoveryAttemptsPerDayPerSource,
    bool ActivateSuccessfulCandidateAutomatically,
    bool RollbackOnFailure);
