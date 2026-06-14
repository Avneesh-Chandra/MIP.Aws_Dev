using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class MarketRate : AuditableEntity
{
    public string Symbol { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public DateOnly TradeDate { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Volume { get; set; }

    public decimal? ChangePercent { get; set; }
}
