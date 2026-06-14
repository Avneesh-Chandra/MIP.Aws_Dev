namespace MIP.Aws.Application.Features.Operator;

public static class DownloadMonitorStatusLabels
{
    public const string Success = "Success";
    public const string SuccessByAiRecovery = "Success By AI Recovery";
    public const string SuccessByAutoAiRecovery = "Success By Auto AI Recovery";

    /// <summary>Legacy alias; prefer <see cref="SuccessByAutoAiRecovery"/>.</summary>
    public const string SuccessWithAutoAiRecovery = SuccessByAutoAiRecovery;
    public const string AutoAiRecoveryQueued = "Auto AI Recovery Queued";
    public const string AutoAiRecoveryRunning = "Auto AI Recovery Running";
    public const string FailedAfterAutoAiRecovery = "Failed After Auto AI Recovery";
    public const string Failed = "Failed";
    public const string InProgress = "In Progress";
    public const string Pending = "Pending";
    public const string ManualActionRequired = "Manual Action Required";
    public const string NoPdfAvailable = "No PDF Available";
    public const string ComplianceBlocked = "Compliance Blocked";
    public const string NoActivity = "No Activity";

    public static bool IsSuccessful(string? status) =>
        string.Equals(status, Success, StringComparison.Ordinal)
        || string.Equals(status, SuccessByAiRecovery, StringComparison.Ordinal)
        || string.Equals(status, SuccessByAutoAiRecovery, StringComparison.Ordinal);
}
