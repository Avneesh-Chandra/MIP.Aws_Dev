namespace MIP.Aws.Application.Features.Operator;

public static class OperatorAuditEvents
{
    public const string DashboardViewed = "Operator.DashboardViewed";
    public const string LatestPdfViewed = "Operator.LatestPdfViewed";
    public const string FailureDetailsViewed = "Operator.FailureDetailsViewed";
    public const string AiRecoveryDetailsViewed = "Operator.AiRecoveryDetailsViewed";
    public const string NoteAdded = "Operator.NoteAdded";
    public const string AdminInformed = "Operator.AdminInformed";
    public const string AdminAcknowledged = "Operator.AdminAcknowledged";
    public const string AdminResolved = "Operator.AdminResolved";
    public const string BatchExecutionStarted = "Operator.BatchExecutionStarted";
    public const string BatchWorkAborted = "Operator.BatchWorkAborted";
}
