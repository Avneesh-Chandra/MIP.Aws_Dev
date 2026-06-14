using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Executive;

/// <summary>Singleton-style configuration row for the daily executive newsletter.</summary>
public sealed class DailyExecutiveBriefSettings : AuditableEntity
{
    public string ToRecipients { get; set; } = string.Empty;

    public string? CcRecipients { get; set; }

    public string? BccRecipients { get; set; }

    public string SendTimeLocal { get; set; } = "07:00";

    public string GenerateTimeLocal { get; set; } = "06:00";

    public string TimeZoneId { get; set; } = "Asia/Bahrain";

    public bool RequireApprovalBeforeSend { get; set; } = true;

    public bool AutoSendApproved { get; set; } = true;

    public bool IncludePdfAttachment { get; set; } = true;

    public bool IncludeExcelAttachment { get; set; }
}
