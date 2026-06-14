namespace MIP.Aws.Domain.Enums;

public enum SourceRecoveryAttemptStatus
{
    AnalysisGenerated = 0,
    CandidateApplied = 1,
    RetryEnqueued = 2,
    Succeeded = 3,
    Failed = 4,
    RolledBack = 5,
    PendingAdminApproval = 6
}
