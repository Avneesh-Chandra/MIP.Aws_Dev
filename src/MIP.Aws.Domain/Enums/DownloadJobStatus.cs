namespace MIP.Aws.Domain.Enums;

public enum DownloadJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    AutoAiRecoveryAnalyzing = 5,
    AutoAiRecoveryApplying = 6,
    AutoAiRecoveryRetrying = 7,
    SuccessWithAutoAiRecovery = 8,
    FailedAfterAutoAiRecovery = 9,
    AutoAiRecoverySkipped = 10,
    ManualInterventionRequired = 11
}
