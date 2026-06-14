using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class EmailLog : AuditableEntity
{
    public Guid? ReportId { get; set; }

    public Report? Report { get; set; }

    public Guid? ReportScheduleId { get; set; }

    public ReportSchedule? ReportSchedule { get; set; }

    public Guid? BriefId { get; set; }

    /// <summary>ACS, Graph, or SMTP.</summary>
    public string Provider { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Primary recipient (To).</summary>
    public string Recipient { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = string.Empty;

    public EmailDeliveryStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public string? MessageId { get; set; }

    public string? ProviderOperationId { get; set; }

    /// <summary>Original To list when development safety redirected delivery.</summary>
    public string? OriginalRecipients { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
