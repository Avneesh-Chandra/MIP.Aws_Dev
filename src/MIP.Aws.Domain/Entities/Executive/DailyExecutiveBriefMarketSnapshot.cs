using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Executive;

public sealed class DailyExecutiveBriefMarketSnapshot : AuditableEntity
{
    public Guid DailyExecutiveBriefId { get; set; }

    public DailyExecutiveBrief? DailyExecutiveBrief { get; set; }

    public string Market { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string Ticker { get; set; } = "GFH";

    public string? ClosingPrice { get; set; }

    public string? PreviousClosingPrice { get; set; }

    public string? ChangePercent { get; set; }

    public string? VolumeTraded { get; set; }

    public bool IsClosed { get; set; }

    public int DisplayOrder { get; set; }
}
