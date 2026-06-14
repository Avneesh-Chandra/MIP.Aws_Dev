namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Outcome of a scheduled report generation and distribution attempt.
/// </summary>
public enum ReportDeliveryStatus
{
    Queued = 0,
    Generating = 1,
    Generated = 2,
    Sending = 3,
    Delivered = 4,
    PartialFailure = 5,
    Failed = 6
}
