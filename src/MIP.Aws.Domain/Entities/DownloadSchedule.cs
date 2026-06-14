using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class DownloadSchedule : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public string CronExpression { get; set; } = "0 0 * * *";

    public string TimeZoneId { get; set; } = "Asia/Bahrain";

    public bool IsEnabled { get; set; } = true;
}
