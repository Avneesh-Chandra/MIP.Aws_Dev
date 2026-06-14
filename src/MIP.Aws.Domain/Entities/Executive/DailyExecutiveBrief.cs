using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Executive;

public sealed class DailyExecutiveBrief : AuditableEntity
{
    public DateOnly BriefDate { get; set; }

    public string Title { get; set; } = string.Empty;

    public DailyExecutiveBriefStatus Status { get; set; } = DailyExecutiveBriefStatus.Draft;

    public DateTimeOffset? GeneratedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public string? PdfStoragePath { get; set; }

    public string? HtmlStoragePath { get; set; }

    public string? LastFailureReason { get; set; }

    public ICollection<DailyExecutiveBriefMarketSnapshot> MarketSnapshots { get; set; } = new List<DailyExecutiveBriefMarketSnapshot>();

    public ICollection<DailyExecutiveBriefItem> Items { get; set; } = new List<DailyExecutiveBriefItem>();

    public ICollection<DailyExecutiveBriefEmailLog> EmailLogs { get; set; } = new List<DailyExecutiveBriefEmailLog>();
}
