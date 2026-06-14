namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Controls whether Hangfire and background paths may deliver email without explicit admin action.
/// </summary>
public sealed class MailAutomationOptions
{
    public const string SectionName = "MailAutomation";

    /// <summary>
    /// When false (default), recurring mail jobs are not registered and automated delivery is blocked.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Allows report-schedule dispatcher and Run schedule now to send report emails.
    /// </summary>
    public bool AllowScheduledReportEmail { get; set; }

    /// <summary>
    /// Allows explicit admin actions such as Send daily brief now.
    /// </summary>
    public bool AllowManualDailyBriefEmail { get; set; } = true;
}
