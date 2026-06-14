namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Why the most recent AI analysis attempt did not complete successfully.
/// Cleared when a run succeeds or is re-queued.
/// </summary>
public enum ArticleAiFailureReason
{
    None = 0,
    NotConfigured = 1,
    NotConnected = 2,
    ServiceError = 3,
    InvalidResponse = 4,
    InsufficientContent = 5
}
