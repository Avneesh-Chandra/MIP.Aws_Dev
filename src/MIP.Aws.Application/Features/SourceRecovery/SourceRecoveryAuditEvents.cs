namespace MIP.Aws.Application.Features.SourceRecovery;

public static class SourceRecoveryAuditEvents
{
    public const string AnalysisGenerated = "SourceRecovery.AnalysisGenerated";
    public const string SuggestionViewed = "SourceRecovery.SuggestionViewed";
    public const string SuggestionApplied = "SourceRecovery.SuggestionApplied";
    public const string RetryStarted = "SourceRecovery.RetryStarted";
    public const string RetrySucceeded = "SourceRecovery.RetrySucceeded";
    public const string RetryFailed = "SourceRecovery.RetryFailed";
    public const string RollbackExecuted = "SourceRecovery.RollbackExecuted";
}
