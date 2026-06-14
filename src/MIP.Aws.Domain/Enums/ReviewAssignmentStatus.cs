namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Lifecycle of a single review assignment (one row per analyst-article pair).
/// </summary>
public enum ReviewAssignmentStatus
{
    Pending = 0,
    Accepted = 1,
    Completed = 2,
    Reassigned = 3,
    Cancelled = 4
}
