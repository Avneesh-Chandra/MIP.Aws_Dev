namespace MIP.Aws.Application.Configuration;

/// <summary>Daily download-monitor schedule, staggered per-source downloads, and status email settings.</summary>
public sealed class PdfEditionSchedulerOptions
{
    public const string SectionName = "PdfEditionScheduler";

    /// <summary>UTC time when the first monitored source download is scheduled (24h, e.g. 04:30).</summary>
    public string FirstSourceScheduleTimeUtc { get; set; } = "04:30";

    /// <summary>Minutes between each monitored source download (e.g. 5 → five sources finish by 04:55 when starting 04:30).</summary>
    public int StaggerIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// UTC time for the legacy fixed-time status email cron (24h, e.g. 05:00).
    /// Used only when <see cref="StatusEmailAfterBatchOnly"/> is false.
    /// </summary>
    public string StatusEmailTimeUtc { get; set; } = "05:00";

    /// <summary>
    /// When true (default), the status email is sent only after a download batch finishes
    /// (scheduled daily batch or operator "Execute PDF download task"), not on a fixed clock time.
    /// </summary>
    public bool StatusEmailAfterBatchOnly { get; set; } = true;

    /// <summary>Enable download-monitor status emails (after batch completion, or on <see cref="StatusEmailTimeUtc"/> when legacy mode).</summary>
    public bool StatusEmailEnabled { get; set; } = true;

    /// <summary>Recipient for the daily download-monitor status email.</summary>
    public string StatusEmailRecipient { get; set; } = "Avneesh.c@almoayyedcomputers.com";

    /// <summary>Legacy local schedule (unused when <see cref="FirstSourceScheduleTimeUtc"/> is set).</summary>
    public string ScheduleTime { get; set; } = "07:30";

    /// <summary>Legacy IANA timezone for <see cref="ScheduleTime"/>.</summary>
    public string TimeZone { get; set; } = "Asia/Bahrain";

    /// <summary>Send an immediate email when a batch PDF job requires operator action (superseded by status email when disabled).</summary>
    public bool NotificationEnabled { get; set; }

    /// <summary>Recipient for legacy manual-action PDF download alerts.</summary>
    public string AdminRecipientEmail { get; set; } = "adminDownloadPdf@gfh.com";

    /// <summary>Public portal base URL for email links (e.g. https://api.your-domain.example).</summary>
    public string? AdminPortalUrl { get; set; }
}
