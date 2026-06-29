namespace MIP.Aws.Domain.Enums;

public enum AutoAiRecoveryRunStatus
{
    Queued = 0,
    Analyzing = 1,
    SuggestionSelected = 2,
    ApplyingCandidate = 3,
    RetryingDownload = 4,
    CandidateSucceeded = 5,
    CandidateFailed = 6,
    RolledBack = 7,
    CompletedSuccess = 8,
    CompletedFailure = 9,
    SkippedUnsafe = 10,
    SkippedCooldown = 11,
    SkippedNoSuggestions = 12,
    SkippedIneligible = 13,
    /// <summary>Baseline selectors/URLs already active; failure is not fixable by re-applying the same patch.</summary>
    SkippedRepeatedBaseline = 14
}
