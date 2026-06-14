using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Executive;

public sealed class DailyExecutiveBriefEmailLog : AuditableEntity
{
    public Guid DailyExecutiveBriefId { get; set; }

    public DailyExecutiveBrief? DailyExecutiveBrief { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public EmailDeliveryStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
