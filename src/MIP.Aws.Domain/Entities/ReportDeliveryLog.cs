using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Audit trail for scheduled report generation and email distribution.
/// </summary>
public sealed class ReportDeliveryLog : AuditableEntity
{
    public Guid? ReportScheduleId { get; set; }

    public ReportSchedule? ReportSchedule { get; set; }

    public Guid? ReportId { get; set; }

    public Report? Report { get; set; }

    public ReportDeliveryStatus Status { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? RecipientsSnapshot { get; set; }
}
