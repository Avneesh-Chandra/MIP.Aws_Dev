namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Lifecycle of a persisted report artifact.
/// </summary>
public enum ReportGenerationStatus
{
    Pending = 0,
    Generating = 1,
    Completed = 2,
    Failed = 3
}
