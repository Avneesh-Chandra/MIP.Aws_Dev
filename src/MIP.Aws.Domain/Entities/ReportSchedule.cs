using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Defines when and how a report is generated and distributed to recipients.
/// </summary>
public sealed class ReportSchedule : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public ReportType ReportType { get; set; }

    public ReportFormat Format { get; set; }

    public ReportScheduleFrequency Frequency { get; set; }

    /// <summary>IANA time zone id (e.g. Asia/Bahrain).</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Local time-of-day in the schedule time zone when the job should run.</summary>
    public TimeOnly RunAtLocal { get; set; } = new TimeOnly(7, 0);

    /// <summary>Optional role name filter for dynamic recipient resolution at send time.</summary>
    public string? TargetRoleName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? NextRunUtc { get; set; }

    public DateTimeOffset? LastRunUtc { get; set; }

    public ICollection<ReportScheduleRecipient> Recipients { get; set; } = new List<ReportScheduleRecipient>();

    public ICollection<ReportDeliveryLog> DeliveryLogs { get; set; } = new List<ReportDeliveryLog>();
}
