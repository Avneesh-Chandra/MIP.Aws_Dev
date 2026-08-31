using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>Runtime mail provider settings (singleton row).</summary>
public sealed class MailSettings : AuditableEntity
{
    public string ActiveProvider { get; set; } = "AzureCommunicationServices";

    public bool DevelopmentSafetyEnabled { get; set; } = true;

    public string? RedirectAllTo { get; set; }

    public string SubjectPrefix { get; set; } = "[GFH-MIP-TEST]";

    public string? StatusEmailRecipient { get; set; }

    /// <summary>Selected recipients for status email; null preserves all configured recipients for legacy rows.</summary>
    public string? StatusEmailSelectedRecipients { get; set; }

    public bool? StatusEmailEnabled { get; set; }

    /// <summary>Whether status email waits for every source to succeed or only to settle.</summary>
    public MailCompletionMode? StatusEmailCompletionMode { get; set; }

    public bool? MailAutomationEnabled { get; set; }

    /// <summary>Recipient for Inform Admin mailto links and intervention alerts.</summary>
    public string? AdminRecipientEmail { get; set; }

    /// <summary>UTC HH:mm for the staggered download-monitor batch (overrides config when set).</summary>
    public string? BatchScheduleTimeUtc { get; set; }

    /// <summary>Daily, Weekdays, or CustomDays.</summary>
    public string? BatchRecurrence { get; set; }

    /// <summary>Hangfire cron days of week (0=Sunday … 6=Saturday), e.g. 1,2,3,4,5.</summary>
    public string? BatchRecurrenceDays { get; set; }

    /// <summary>Whether the separate all-success PDF Download mail is enabled.</summary>
    public bool? PdfDownloadMailEnabled { get; set; }

    /// <summary>Recipients for the separate PDF Download mail.</summary>
    public string? PdfDownloadMailRecipient { get; set; }

    /// <summary>Selected recipients for PDF Download mail; null preserves all configured recipients for legacy rows.</summary>
    public string? PdfDownloadMailSelectedRecipients { get; set; }

    /// <summary>Whether PDF Download mail requires every source to succeed or only to settle.</summary>
    public MailCompletionMode? PdfDownloadMailCompletionMode { get; set; }

    /// <summary>Subject for the separate PDF Download mail.</summary>
    public string? PdfDownloadMailSubject { get; set; }

    /// <summary>UTC HH:mm for the separate PDF Download mail.</summary>
    public string? PdfDownloadMailTimeUtc { get; set; }
}
