using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>Runtime mail provider settings (singleton row).</summary>
public sealed class MailSettings : AuditableEntity
{
    public string ActiveProvider { get; set; } = "AzureCommunicationServices";

    public bool DevelopmentSafetyEnabled { get; set; } = true;

    public string? RedirectAllTo { get; set; }

    public string SubjectPrefix { get; set; } = "[GFH-MIP-TEST]";

    public string? StatusEmailRecipient { get; set; }

    public bool? StatusEmailEnabled { get; set; }

    public bool? MailAutomationEnabled { get; set; }

    /// <summary>Recipient for Inform Admin mailto links and intervention alerts.</summary>
    public string? AdminRecipientEmail { get; set; }

    /// <summary>UTC HH:mm for the staggered download-monitor batch (overrides config when set).</summary>
    public string? BatchScheduleTimeUtc { get; set; }

    /// <summary>Daily, Weekdays, or CustomDays.</summary>
    public string? BatchRecurrence { get; set; }

    /// <summary>Hangfire cron days of week (0=Sunday … 6=Saturday), e.g. 1,2,3,4,5.</summary>
    public string? BatchRecurrenceDays { get; set; }
}
