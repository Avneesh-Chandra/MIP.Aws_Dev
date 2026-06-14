namespace MIP.Aws.Application.Features.AutoAiRecovery;

public static class AutoAiRecoveryAuditEvents
{
    public const string Queued = "AutoAiRecovery.Queued";
    public const string Started = "AutoAiRecovery.Started";
    public const string SuggestionRanked = "AutoAiRecovery.SuggestionRanked";
    public const string SuggestionSkippedUnsafe = "AutoAiRecovery.SuggestionSkippedUnsafe";
    public const string CandidateCreated = "AutoAiRecovery.CandidateCreated";
    public const string PatchApplied = "AutoAiRecovery.PatchApplied";
    public const string RetryStarted = "AutoAiRecovery.RetryStarted";
    public const string RetrySucceeded = "AutoAiRecovery.RetrySucceeded";
    public const string RetryFailed = "AutoAiRecovery.RetryFailed";
    public const string CandidateActivated = "AutoAiRecovery.CandidateActivated";
    public const string RollbackExecuted = "AutoAiRecovery.RollbackExecuted";
    public const string CompletedSuccess = "AutoAiRecovery.CompletedSuccess";
    public const string CompletedFailure = "AutoAiRecovery.CompletedFailure";
}
