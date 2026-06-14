namespace MIP.Aws.Application.Configuration;

public sealed class DailyExecutiveBriefOptions
{
    public const string SectionName = "DailyExecutiveBrief";

    public string GenerateTime { get; set; } = "06:00";

    public string SendTime { get; set; } = "07:30";

    public string TimeZone { get; set; } = "Asia/Bahrain";

    public bool RequireApprovalBeforeSend { get; set; } = true;

    public bool AutoSendApproved { get; set; } = true;
}
